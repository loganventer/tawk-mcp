using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Okf;
using Tawk.Mcp.Engines;
using Tawk.Mcp.Engines.Knowledge;
using Tawk.Mcp.Engines.Memory;
using Tawk.Mcp.Managers.Memory;
using Tawk.Mcp.ResourceAccess.Memory;

namespace Tawk.Mcp.Managers.Knowledge;

public sealed class KnowledgeManager(
    IOkfStore knowledge,
    IKnowledgeSubjects subjects,
    IKnowledgeFormatter formatter,
    IUntrustedTextFence fence,
    IMemoryWriteGuard guard,
    OkfProducer producer,
    TimeProvider clock,
    IAccountTag? accountTag = null) : IKnowledgeManager
{
    private const string Label = "knowledge from tawk-mcp's memory, written by the user or by an agent that read their chats";
    private const int MaxTextLength = 2000;
    private const int MaxLabelLength = 60;
    private const int MaxTags = 8;
    private const int MaxListed = 100;

    // An inference lapses unless it is given a life of its own; what someone stated stays until changed.
    private static readonly TimeSpan InferredLifetime = TimeSpan.FromDays(180);

    public async Task<string> RecordObservationAsync(
        string about, string text, string source, IReadOnlyList<string>? tags, double? confidence, bool sensitive, string? evidence,
        int? staleAfterDays, CancellationToken cancellationToken)
    {
        guard.EnsureWritable();
        var from = FactSources.Parse(source);
        var sure = Confidence(confidence, from);
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxTextLength)
        {
            throw new MemoryException($"An observation is 1 to {MaxTextLength} characters.");
        }

        if (sensitive && from != FactSource.User)
        {
            throw new MemoryException("Nothing was saved. Sensitive matters such as health or beliefs can only come from the user, with source user.");
        }

        if (staleAfterDays is <= 0)
        {
            throw new MemoryException("stale_after_days is 1 or more.");
        }

        var labels = Tags(tags);
        var subject = await subjects.ResolveAsync(about, true, cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var id = OkfIds.ObservationAbout(subject.Id, now);
        while (await knowledge.GetAsync(id, cancellationToken).ConfigureAwait(false) is not null)
        {
            now = now.AddMilliseconds(1);
            id = OkfIds.ObservationAbout(subject.Id, now);
        }

        var account = accountTag is null ? null : await accountTag.CurrentAsync(cancellationToken).ConfigureAwait(false);
        var lifetime = staleAfterDays is { } days ? TimeSpan.FromDays(days) : from == FactSource.Inferred ? InferredLifetime : (TimeSpan?)null;
        await knowledge.UpsertAsync(
            new OkfConcept(
                id, OkfTypes.Observation, null, null, null, labels, OkfActors.For(from, OkfIds.JidOf(subject.Id), producer.Version), now,
                from == FactSource.User ? [new OkfVerification(OkfActors.Self, now)] : [], OkfStatus.Stable, now + lifetime, [],
                OkfExtras.Write(new ObservationDetails(from, sure, sensitive, Trim(evidence)) { Account = account }), text.Trim(), now),
            cancellationToken).ConfigureAwait(false);
        await knowledge.UpsertLinkAsync(new OkfLink(id, subject.Id, OkfIds.About, null, from, sure, now) { Account = account }, cancellationToken).ConfigureAwait(false);
        return $"Observation {id} recorded about {subject.Name}.";
    }

    public async Task<string> ListObservationsAsync(string? about, string? tag, string? query, bool includeSensitive, CancellationToken cancellationToken)
    {
        var subject = string.IsNullOrWhiteSpace(about) ? null : await subjects.ResolveAsync(about, false, cancellationToken).ConfigureAwait(false);
        var found = await knowledge.ListAsync(
            OkfTypes.Observation, subject?.Id, Trim(tag)?.ToLowerInvariant(), Trim(query), cancellationToken).ConfigureAwait(false);
        var (shown, hidden) = await ViewsAsync(found, includeSensitive, cancellationToken).ConfigureAwait(false);
        return fence.Wrap(Label, formatter.Observations(shown, hidden, clock.GetUtcNow()));
    }

    public async Task<string> RecordRelationAsync(
        string first, string label, string second, string source, double? confidence, string? note, CancellationToken cancellationToken)
    {
        guard.EnsureWritable();
        var by = FactSources.Parse(source);
        var sure = Confidence(confidence, by);
        var kind = RelationLabel(label);
        if (note is { Length: > MaxTextLength })
        {
            throw new MemoryException($"A note is at most {MaxTextLength} characters.");
        }

        var start = await subjects.ResolveAsync(first, true, cancellationToken).ConfigureAwait(false);
        var end = await subjects.ResolveAsync(second, true, cancellationToken).ConfigureAwait(false);
        if (start.Id == end.Id)
        {
            throw new MemoryException("A relation is between two different things.");
        }

        var current = (await knowledge.GetLinksFromAsync(start.Id, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(l => l.ToId == end.Id && l.Label == kind);
        if (current is not null && current.Source > by)
        {
            return $"Kept what was already stated by {OkfExtras.SourceName(current.Source)}: {start.Name} is {kind} {end.Name}. "
                + "An inference never replaces what someone stated.";
        }

        var account = accountTag is null ? null : await accountTag.CurrentAsync(cancellationToken).ConfigureAwait(false);
        await knowledge.UpsertLinkAsync(
            new OkfLink(start.Id, end.Id, kind, Trim(note), by, sure, clock.GetUtcNow()) { Account = account }, cancellationToken).ConfigureAwait(false);
        return $"Recorded: {start.Name} is {kind} {end.Name}.";
    }

    public async Task<string> GetKnowledgeAsync(string subject, bool includeSensitive, CancellationToken cancellationToken)
    {
        var target = await subjects.ResolveAsync(subject, false, cancellationToken).ConfigureAwait(false);
        var concept = await knowledge.GetAsync(target.Id, cancellationToken).ConfigureAwait(false);
        if (concept is null)
        {
            return $"Nothing is known about {target.Name} yet. record_observation and record_relation start it.";
        }

        var names = new Dictionary<string, string>(StringComparer.Ordinal) { [concept.Id] = concept.Title ?? target.Name };
        var relations = new List<RelatedConcept>();
        foreach (var link in await knowledge.GetLinksFromAsync(concept.Id, cancellationToken).ConfigureAwait(false))
        {
            relations.Add(new RelatedConcept(link, link.ToId, await NameAsync(link.ToId, names, cancellationToken).ConfigureAwait(false), true));
        }

        foreach (var link in (await knowledge.GetLinksToAsync(concept.Id, cancellationToken).ConfigureAwait(false)).Where(l => l.Label != OkfIds.About))
        {
            relations.Add(new RelatedConcept(link, link.FromId, await NameAsync(link.FromId, names, cancellationToken).ConfigureAwait(false), false));
        }

        var about = await knowledge.ListAsync(OkfTypes.Observation, concept.Id, null, null, cancellationToken).ConfigureAwait(false);
        var (shown, hidden) = await ViewsAsync(about, includeSensitive, cancellationToken).ConfigureAwait(false);
        return fence.Wrap(Label, formatter.Concept(concept, relations, shown, hidden, clock.GetUtcNow()));
    }

    public async Task<string> ForgetObservationAsync(string id, CancellationToken cancellationToken)
    {
        guard.EnsureWritable();
        var wanted = id?.Trim() ?? string.Empty;
        var concept = await knowledge.GetAsync(wanted, cancellationToken).ConfigureAwait(false);
        if (concept is null || concept.Type != OkfTypes.Observation)
        {
            throw new MemoryException($"There is no observation with the id {wanted}. list_observations shows them.");
        }

        await knowledge.DeleteAsync(wanted, cancellationToken).ConfigureAwait(false);
        return $"Forgot observation {wanted}.";
    }

    public async Task<string> ForgetRelationAsync(string first, string label, string second, CancellationToken cancellationToken)
    {
        guard.EnsureWritable();
        var kind = RelationLabel(label);
        var start = await subjects.ResolveAsync(first, false, cancellationToken).ConfigureAwait(false);
        var end = await subjects.ResolveAsync(second, false, cancellationToken).ConfigureAwait(false);
        return await knowledge.DeleteLinkAsync(start.Id, end.Id, kind, cancellationToken).ConfigureAwait(false)
            ? $"Forgot that {start.Name} is {kind} {end.Name}."
            : $"Nothing was stored saying {start.Name} is {kind} {end.Name}.";
    }

    private static double Confidence(double? confidence, FactSource source)
    {
        var sure = confidence ?? (source is FactSource.User or FactSource.Contact ? 1.0 : 0.5);
        return sure is < 0 or > 1 ? throw new MemoryException("confidence is from 0 to 1.") : sure;
    }

    private static string RelationLabel(string label)
    {
        var kind = string.Join(' ', (label ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
        if (kind.Length == 0 || kind.Length > MaxLabelLength || kind == OkfIds.About)
        {
            throw new MemoryException($"label says what the first is to the second in a few words, such as \"spouse of\" or \"works with\", at most {MaxLabelLength} characters.");
        }

        return kind;
    }

    private static List<string> Tags(IReadOnlyList<string>? tags)
    {
        var labels = (tags ?? [])
            .Select(t => t?.Trim().ToLowerInvariant() ?? string.Empty)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (labels.Count > MaxTags || labels.Any(t => t.Length > 40 || t.Any(char.IsWhiteSpace)))
        {
            throw new MemoryException($"At most {MaxTags} tags, each one word of up to 40 characters.");
        }

        return labels.Count == 0 ? ["general"] : labels;
    }

    private static string? Trim(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task<(IReadOnlyList<ObservationView> Shown, int Hidden)> ViewsAsync(
        IReadOnlyList<OkfConcept> observations, bool includeSensitive, CancellationToken cancellationToken)
    {
        var visible = includeSensitive ? observations : observations.Where(o => !OkfExtras.Read(o).Sensitive).ToList();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var views = new List<ObservationView>();
        foreach (var observation in visible.Take(MaxListed))
        {
            var about = new List<string>();
            foreach (var link in (await knowledge.GetLinksFromAsync(observation.Id, cancellationToken).ConfigureAwait(false)).Where(l => l.Label == OkfIds.About))
            {
                about.Add(await NameAsync(link.ToId, names, cancellationToken).ConfigureAwait(false));
            }

            views.Add(new ObservationView(observation, about));
        }

        return (views, observations.Count - visible.Count);
    }

    private async Task<string> NameAsync(string id, Dictionary<string, string> names, CancellationToken cancellationToken)
    {
        if (!names.TryGetValue(id, out var name))
        {
            name = (await knowledge.GetAsync(id, cancellationToken).ConfigureAwait(false))?.Title ?? id;
            names[id] = name;
        }

        return name;
    }
}
