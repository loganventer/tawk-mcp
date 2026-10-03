using System.Globalization;
using System.Text;
using System.Text.Json;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Engines;
using Tawk.Mcp.Engines.Memory;
using Tawk.Mcp.ResourceAccess;
using Tawk.Mcp.ResourceAccess.Memory;

namespace Tawk.Mcp.Managers.Memory;

public sealed class ContactProfileManager(
    IContactStore contacts,
    IVoiceStore voices,
    ICategoryEnsurer categories,
    ITawkChatSource chats,
    IContactFieldCatalog catalog,
    IMemoryFormatter formatter,
    IUntrustedTextFence fence,
    IMemoryWriteGuard guard,
    TimeProvider clock,
    IAccountTag? accountTag = null) : IContactProfileManager
{
    private const string ProfileLabel = "a contact profile from tawk-mcp's memory, written by the user or by an agent that read their chats";
    private const int MaxNoteLength = 2000;

    public string ListFields() => formatter.Fields(catalog.All);

    public async Task<string> GetContactAsync(string chat, bool includeSensitive, CancellationToken cancellationToken)
    {
        var target = await chats.ResolveAsync(chat, cancellationToken).ConfigureAwait(false);
        var contact = await contacts.GetAsync(target.Jid, cancellationToken).ConfigureAwait(false);
        if (contact is null)
        {
            return $"Nothing is stored about {target.Name} <{target.Jid}> yet. set_contact_fields and add_contact_note start a profile.";
        }

        await contacts.PurgeExpiredAsync(clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        var facts = await contacts.GetFactsAsync(target.Jid, cancellationToken).ConfigureAwait(false);
        var shown = includeSensitive ? facts : facts.Where(f => !f.Sensitive).ToList();
        var text = formatter.Contact(
            contact,
            await contacts.GetCategoriesAsync(target.Jid, cancellationToken).ConfigureAwait(false),
            shown,
            await contacts.GetNotesAsync(target.Jid, cancellationToken).ConfigureAwait(false),
            facts.Count - shown.Count);
        return fence.Wrap(ProfileLabel, text);
    }

    public async Task<string> ListContactsAsync(string? category, string? query, CancellationToken cancellationToken)
    {
        var path = category is null ? null : CategoryPaths.Normalise(category);
        var found = await contacts.ListAsync(path, string.IsNullOrWhiteSpace(query) ? null : query.Trim(), cancellationToken).ConfigureAwait(false);
        return fence.Wrap(ProfileLabel, formatter.Contacts(found));
    }

    public async Task<string> SetFieldsAsync(string chat, string fieldsJson, string source, double? confidence, string? evidence, CancellationToken cancellationToken)
    {
        guard.EnsureWritable();
        var from = FactSources.Parse(source);
        var sure = confidence ?? (from is FactSource.User or FactSource.Contact ? 1.0 : 0.5);
        if (sure is < 0 or > 1)
        {
            throw new MemoryException("confidence is from 0 to 1.");
        }

        var fields = ParseFields(fieldsJson);
        var checks = fields.Select(p => (p.Key, p.Value, Check: catalog.Check(p.Key, p.Value, from))).ToList();
        var problems = checks.Where(c => !c.Check.Ok).Select(c => c.Check.Problem).ToList();
        if (problems.Count > 0)
        {
            throw new MemoryException("Nothing was saved. " + string.Join(" ", problems));
        }

        var target = await chats.ResolveAsync(chat, cancellationToken).ConfigureAwait(false);
        await EnsureContactAsync(target, cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var existing = (await contacts.GetFactsAsync(target.Jid, cancellationToken).ConfigureAwait(false))
            .ToDictionary(f => f.Field, StringComparer.Ordinal);
        var account = accountTag is null ? null : await accountTag.CurrentAsync(cancellationToken).ConfigureAwait(false);
        var saved = new List<string>();
        var kept = new List<string>();
        foreach (var (_, value, check) in checks)
        {
            var field = check.Field!;
            if (existing.TryGetValue(field.Name, out var current) && current.Source > from && !Lapsed(current, now))
            {
                kept.Add($"{field.Name} (already stated by {current.Source.ToString().ToLowerInvariant()})");
                continue;
            }

            var lifetime = field.Lifetime ?? (from is FactSource.Inferred or FactSource.Imported ? field.InferredLifetime : null);
            await contacts.UpsertFactAsync(
                new ContactFact(target.Jid, field.Name, value.GetRawText(), from, sure, Trim(evidence), field.Sensitive, now, now + lifetime)
                {
                    Account = account,
                },
                cancellationToken).ConfigureAwait(false);
            saved.Add(field.Name);
        }

        var result = new StringBuilder($"Saved for {target.Name}: {(saved.Count == 0 ? "nothing" : string.Join(", ", saved))}.");
        if (kept.Count > 0)
        {
            result.Append(" Kept the stronger value for ").Append(string.Join(", ", kept)).Append(": an inference never replaces what someone stated.");
        }

        return result.ToString();
    }

    public async Task<string> AddNoteAsync(string chat, string text, string source, CancellationToken cancellationToken)
    {
        guard.EnsureWritable();
        var from = FactSources.Parse(source);
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxNoteLength)
        {
            throw new MemoryException($"A note is 1 to {MaxNoteLength} characters.");
        }

        var target = await chats.ResolveAsync(chat, cancellationToken).ConfigureAwait(false);
        await EnsureContactAsync(target, cancellationToken).ConfigureAwait(false);
        var id = await contacts.AddNoteAsync(new ContactNote(0, target.Jid, text.Trim(), from, clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        return string.Create(CultureInfo.InvariantCulture, $"Note {id} added for {target.Name}.");
    }

    public async Task<string> ForgetFieldAsync(string chat, string field, CancellationToken cancellationToken)
    {
        guard.EnsureWritable();
        var target = await chats.ResolveAsync(chat, cancellationToken).ConfigureAwait(false);
        var name = catalog.Find(field)?.Name ?? field;
        return await contacts.DeleteFactAsync(target.Jid, name, cancellationToken).ConfigureAwait(false)
            ? $"Forgot {name} for {target.Name}."
            : $"Nothing was stored as {name} for {target.Name}.";
    }

    public async Task<string> SetCategoriesAsync(string chat, IReadOnlyList<string> categoryPaths, string? voice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(categoryPaths);
        guard.EnsureWritable();
        var target = await chats.ResolveAsync(chat, cancellationToken).ConfigureAwait(false);
        var paths = new List<string>();
        foreach (var category in categoryPaths)
        {
            paths.Add(await categories.EnsureAsync(category, cancellationToken).ConfigureAwait(false));
        }

        var contact = await EnsureContactAsync(target, cancellationToken).ConfigureAwait(false);
        if (voice is not null)
        {
            var name = voice.Trim().ToLowerInvariant();
            if (name.Length > 0 && await voices.GetAsync(name, cancellationToken).ConfigureAwait(false) is null)
            {
                throw new MemoryException($"There is no voice called {voice}.");
            }

            await contacts.UpsertAsync(contact with { Voice = name.Length == 0 ? null : name, Updated = clock.GetUtcNow() }, cancellationToken).ConfigureAwait(false);
        }

        await contacts.SetCategoriesAsync(target.Jid, paths, cancellationToken).ConfigureAwait(false);
        return $"{target.Name} is now in: {(paths.Count == 0 ? "no categories" : string.Join(", ", paths))}."
            + (voice is null ? string.Empty : $" Voice: {(voice.Trim().Length == 0 ? "the default" : voice.Trim().ToLowerInvariant())}.");
    }

    public async Task<string> DeleteContactAsync(string chat, IUserConfirmation? confirmation, CancellationToken cancellationToken)
    {
        guard.EnsureWritable();
        var target = await chats.ResolveAsync(chat, cancellationToken).ConfigureAwait(false);
        if (await contacts.GetAsync(target.Jid, cancellationToken).ConfigureAwait(false) is null)
        {
            throw new MemoryException($"Nothing is stored about {target.Name}.");
        }

        await MemoryConfirmations.EnsureAsync(
            confirmation, $"forget everything tawk-mcp has stored about {target.Name}: every field, note and category", cancellationToken).ConfigureAwait(false);
        await contacts.DeleteAsync(target.Jid, cancellationToken).ConfigureAwait(false);
        return $"Forgot everything about {target.Name}.";
    }

    public async Task<string> DueFollowUpsAsync(int withinDays, CancellationToken cancellationToken)
    {
        var until = DateOnly.FromDateTime(clock.GetLocalNow().DateTime).AddDays(Math.Clamp(withinDays, 0, 365));
        var lines = new List<string>();
        foreach (var fact in await contacts.FindFactsAsync("follow_ups", cancellationToken).ConfigureAwait(false))
        {
            var contact = await contacts.GetAsync(fact.Jid, cancellationToken).ConfigureAwait(false);
            foreach (var (text, due) in FollowUps(fact.ValueJson))
            {
                if (due is null || due <= until)
                {
                    lines.Add($"{contact?.DisplayName ?? fact.Jid} <{fact.Jid}>: {text}" + (due is { } d ? $" (due {d:yyyy-MM-dd})" : string.Empty));
                }
            }
        }

        return fence.Wrap(ProfileLabel, lines.Count == 0 ? "No follow-ups are due." : string.Join('\n', lines));
    }

    private static Dictionary<string, JsonElement> ParseFields(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.EnumerateObject().Any())
            {
                throw new MemoryException("fields must be a JSON object such as {\"relation\": \"friend\", \"closeness\": 4}.");
            }

            return document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal);
        }
        catch (JsonException ex)
        {
            throw new MemoryException("fields is not valid JSON: " + ex.Message, ex);
        }
    }

    private static IEnumerable<(string Text, DateOnly? Due)> FollowUps(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            DateOnly? due = item.TryGetProperty("due", out var d) && d.ValueKind == JsonValueKind.String
                && DateOnly.TryParseExact(d.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed
                : null;
            yield return (text.GetString()!, due);
        }
    }

    private static bool Lapsed(ContactFact fact, DateTimeOffset now) => fact.Expires is { } expires && expires <= now;

    private static string? Trim(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task<ContactRecord> EnsureContactAsync(ChatRef target, CancellationToken cancellationToken)
    {
        var contact = await contacts.GetAsync(target.Jid, cancellationToken).ConfigureAwait(false);
        if (contact is not null)
        {
            return contact;
        }

        contact = new ContactRecord(target.Jid, target.Name, null, clock.GetUtcNow());
        await contacts.UpsertAsync(contact, cancellationToken).ConfigureAwait(false);
        return contact;
    }
}
