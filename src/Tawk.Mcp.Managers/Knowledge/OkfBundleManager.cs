using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Okf;
using Tawk.Mcp.Engines.Knowledge;
using Tawk.Mcp.Engines.Memory;
using Tawk.Mcp.ResourceAccess.Memory;

namespace Tawk.Mcp.Managers.Knowledge;

public sealed class OkfBundleManager(
    IOkfStore knowledge,
    IContactStore contacts,
    IOkfBundleWriter writer,
    IOkfBundleReader reader,
    IOkfBundleFiles files,
    IKnowledgePrecedence precedence,
    IMemoryWriteGuard guard,
    TimeProvider clock) : IOkfBundleManager
{
    private const int ProblemsShown = 5;

    public async Task<string> ExportAsync(string directory, bool includeSensitive, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var concepts = (await knowledge.ListAsync(null, null, null, null, cancellationToken).ConfigureAwait(false))
            .Where(c => includeSensitive || !OkfExtras.Read(c).Sensitive)
            .ToList();
        var kept = concepts.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var links = (await knowledge.ListLinksAsync(cancellationToken).ConfigureAwait(false)).Where(l => kept.Contains(l.FromId)).ToList();
        var facts = new List<ContactFact>();
        foreach (var jid in concepts.Select(c => OkfIds.JidOf(c.Id)).OfType<string>())
        {
            facts.AddRange((await contacts.GetFactsAsync(jid, cancellationToken).ConfigureAwait(false))
                .Where(f => (includeSensitive || !f.Sensitive) && !(f.Expires <= now)));
        }

        var written = writer.Write(new OkfBundle(concepts, links, facts));
        await files.WriteAsync(directory, written.Files, cancellationToken).ConfigureAwait(false);
        var exported = concepts.Count - written.Skipped.Count;
        return $"Exported {exported} concept(s), {links.Count} relation(s) and {facts.Count} profile field(s) to {Path.GetFullPath(directory)}."
            + (includeSensitive ? string.Empty : " Sensitive ones were left out.")
            + (written.Skipped.Count == 0 ? string.Empty : $" {written.Skipped.Count} concept(s) were skipped: their ids cannot be file names.");
    }

    public async Task<string> ImportAsync(string directory, CancellationToken cancellationToken)
    {
        guard.EnsureWritable();
        var reading = reader.Read(await files.ReadAsync(directory, cancellationToken).ConfigureAwait(false), clock.GetUtcNow());
        var taken = 0;
        foreach (var concept in reading.Bundle.Concepts)
        {
            // A contact's concept needs its contact, which also holds the profile fields.
            if (OkfIds.JidOf(concept.Id) is { } jid && await contacts.GetAsync(jid, cancellationToken).ConfigureAwait(false) is null)
            {
                await contacts.UpsertAsync(new ContactRecord(jid, concept.Title, null, concept.Updated), cancellationToken).ConfigureAwait(false);
                await knowledge.UpsertAsync(concept, cancellationToken).ConfigureAwait(false);
                taken++;
                continue;
            }

            var current = await knowledge.GetAsync(concept.Id, cancellationToken).ConfigureAwait(false);
            if (current is null || precedence.Replaces(OkfExtras.Read(concept).Source, concept.Updated, OkfExtras.Read(current).Source, current.Updated))
            {
                await knowledge.UpsertAsync(concept, cancellationToken).ConfigureAwait(false);
                taken++;
            }
        }

        var relations = 0;
        foreach (var group in reading.Bundle.Links.GroupBy(l => l.FromId, StringComparer.Ordinal))
        {
            if (await knowledge.GetAsync(group.Key, cancellationToken).ConfigureAwait(false) is null)
            {
                continue;
            }

            var existing = await knowledge.GetLinksFromAsync(group.Key, cancellationToken).ConfigureAwait(false);
            foreach (var link in group)
            {
                var current = existing.FirstOrDefault(l => l.ToId == link.ToId && l.Label == link.Label);
                if (current is null || precedence.Replaces(link.Source, link.Updated, current.Source, current.Updated))
                {
                    await knowledge.UpsertLinkAsync(link, cancellationToken).ConfigureAwait(false);
                    relations++;
                }
            }
        }

        var fields = 0;
        foreach (var group in reading.Bundle.Facts.GroupBy(f => f.Jid, StringComparer.Ordinal))
        {
            if (await contacts.GetAsync(group.Key, cancellationToken).ConfigureAwait(false) is null)
            {
                continue;
            }

            var existing = await contacts.GetFactsAsync(group.Key, cancellationToken).ConfigureAwait(false);
            foreach (var fact in group)
            {
                var current = existing.FirstOrDefault(f => f.Field == fact.Field);
                if (current is null || precedence.Replaces(fact.Source, fact.Updated, current.Source, current.Updated))
                {
                    await contacts.UpsertFactAsync(fact, cancellationToken).ConfigureAwait(false);
                    fields++;
                }
            }
        }

        var bundle = reading.Bundle;
        return $"Read {bundle.Concepts.Count} concept(s), {bundle.Links.Count} relation(s) and {bundle.Facts.Count} profile field(s); "
            + $"took {taken}, {relations} and {fields}. The rest were already here, or what is here is stronger or newer."
            + (reading.Problems.Count == 0
                ? string.Empty
                : $"\n{reading.Problems.Count} file(s) were not read:\n  " + string.Join("\n  ", reading.Problems.Take(ProblemsShown)));
    }
}
