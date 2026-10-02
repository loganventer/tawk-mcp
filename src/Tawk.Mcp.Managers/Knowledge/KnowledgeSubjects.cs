using System.Text.RegularExpressions;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Okf;
using Tawk.Mcp.ResourceAccess;
using Tawk.Mcp.ResourceAccess.Memory;

namespace Tawk.Mcp.Managers.Knowledge;

public sealed partial class KnowledgeSubjects(
    IOkfStore knowledge, IContactStore contacts, ITawkChatSource chats, OkfProducer producer, TimeProvider clock) : IKnowledgeSubjects
{
    private const string SelfName = "the user";

    public async Task<KnowledgeSubject> ResolveAsync(string subject, bool create, CancellationToken cancellationToken)
    {
        var wanted = subject?.Trim() ?? string.Empty;
        if (wanted.Length == 0)
        {
            throw new MemoryException("Say who or what it is about: a chat's jid or name, self, or a topic such as concepts/work.");
        }

        if (wanted.Equals(OkfIds.Self, StringComparison.OrdinalIgnoreCase))
        {
            return await ConceptAsync(OkfIds.Self, OkfTypes.Self, SelfName, create, cancellationToken).ConfigureAwait(false);
        }

        if (wanted.StartsWith(OkfIds.ConceptPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var id = wanted.ToLowerInvariant();
            if (!TopicId().IsMatch(id))
            {
                throw new MemoryException("A topic id is concepts/ then lowercase letters, digits and dashes, such as concepts/cape-town-trip.");
            }

            return await ConceptAsync(id, OkfTypes.Concept, id[OkfIds.ConceptPrefix.Length..].Replace('-', ' '), create, cancellationToken).ConfigureAwait(false);
        }

        // A contact is resolved through tawk, so hidden, locked and disallowed chats are refused as usual.
        var target = await chats.ResolveAsync(OkfIds.JidOf(wanted) ?? wanted, cancellationToken).ConfigureAwait(false);
        if (create && await contacts.GetAsync(target.Jid, cancellationToken).ConfigureAwait(false) is null)
        {
            await contacts.UpsertAsync(new ContactRecord(target.Jid, target.Name, null, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        }

        return new KnowledgeSubject(OkfIds.Contact(target.Jid), target.Name);
    }

    [GeneratedRegex("^concepts/[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex TopicId();

    private async Task<KnowledgeSubject> ConceptAsync(string id, string type, string title, bool create, CancellationToken cancellationToken)
    {
        var existing = await knowledge.GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (existing is null && create)
        {
            var now = clock.GetUtcNow();
            await knowledge.UpsertAsync(
                new OkfConcept(id, type, title, null, null, [], producer.Actor, now, [], OkfStatus.Stable, null, [], "{}", string.Empty, now),
                cancellationToken).ConfigureAwait(false);
        }

        return new KnowledgeSubject(id, existing?.Title ?? title);
    }
}
