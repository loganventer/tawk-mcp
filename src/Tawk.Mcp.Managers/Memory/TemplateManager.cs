using System.Text;
using System.Text.Json;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Engines;
using Tawk.Mcp.Engines.Memory;
using Tawk.Mcp.ResourceAccess;
using Tawk.Mcp.ResourceAccess.Memory;

namespace Tawk.Mcp.Managers.Memory;

public sealed class TemplateManager(
    ITemplateStore templates,
    IContactStore contacts,
    IVoiceStore voices,
    ICategoryEnsurer categories,
    ITawkChatSource chats,
    ITemplateRenderer renderer,
    IVoiceSelector selector,
    IVoiceChecker checker,
    IMemoryFormatter formatter,
    IUntrustedTextFence fence,
    IMemoryWriteGuard guard,
    TimeProvider clock) : ITemplateManager
{
    private const string TemplateLabel = "a response template from tawk-mcp's memory, saved by the user or an agent";
    private const int MaxBodyLength = 4000;

    public async Task<string> ListAsync(string? category, CancellationToken cancellationToken)
    {
        var path = category is null ? null : CategoryPaths.Normalise(category);
        return fence.Wrap(TemplateLabel, formatter.Templates(await templates.ListAsync(path, cancellationToken).ConfigureAwait(false)));
    }

    public async Task<string> GetAsync(string name, CancellationToken cancellationToken)
    {
        var found = await RequireAsync(name, cancellationToken).ConfigureAwait(false);
        return fence.Wrap(TemplateLabel, formatter.Template(found, renderer.Placeholders(found.Body)));
    }

    public async Task<string> SetAsync(
        string name, string body, string? description, string? category, string? voice, string? language, CancellationToken cancellationToken)
    {
        guard.EnsureWritable();
        var clean = Name(name);
        if (string.IsNullOrWhiteSpace(body) || body.Length > MaxBodyLength)
        {
            throw new MemoryException($"A template body is 1 to {MaxBodyLength} characters.");
        }

        var path = string.IsNullOrWhiteSpace(category) ? null : await categories.EnsureAsync(category, cancellationToken).ConfigureAwait(false);
        var voiceName = string.IsNullOrWhiteSpace(voice) ? null : voice.Trim().ToLowerInvariant();
        if (voiceName is not null && await voices.GetAsync(voiceName, cancellationToken).ConfigureAwait(false) is null)
        {
            throw new MemoryException($"There is no voice called {voice}.");
        }

        await templates.UpsertAsync(
            new ResponseTemplate(clean, Blank(description), path, voiceName, Blank(language), body.Trim(), clock.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
        var placeholders = renderer.Placeholders(body);
        return $"Template {clean} saved" + (placeholders.Count == 0 ? "." : $" with placeholders {string.Join(", ", placeholders)}.");
    }

    public async Task<string> DeleteAsync(string name, IUserConfirmation? confirmation, CancellationToken cancellationToken)
    {
        guard.EnsureWritable();
        var found = await RequireAsync(name, cancellationToken).ConfigureAwait(false);
        await MemoryConfirmations.EnsureAsync(confirmation, $"delete the response template {found.Name}", cancellationToken).ConfigureAwait(false);
        await templates.DeleteAsync(found.Name, cancellationToken).ConfigureAwait(false);
        return $"Template {found.Name} deleted.";
    }

    public async Task<string> RenderAsync(string name, string? chat, string? valuesJson, CancellationToken cancellationToken)
    {
        var found = await RequireAsync(name, cancellationToken).ConfigureAwait(false);
        var target = chat is null ? null : await chats.ResolveAsync(chat, cancellationToken).ConfigureAwait(false);
        var (rendering, notes) = await FillAsync(found, target, valuesJson, cancellationToken).ConfigureAwait(false);
        var text = new StringBuilder(rendering.Text);
        if (rendering.Missing.Count > 0)
        {
            text.Append("\n\nNo value for: ").Append(string.Join(", ", rendering.Missing)).Append(". Pass them in values, or store them on the contact.");
        }

        text.Append(notes);
        return fence.Wrap(TemplateLabel, text.ToString());
    }

    public async Task<PreparedDraft> PrepareDraftAsync(string name, string chat, string? valuesJson, CancellationToken cancellationToken)
    {
        var found = await RequireAsync(name, cancellationToken).ConfigureAwait(false);
        var target = await chats.ResolveAsync(chat, cancellationToken).ConfigureAwait(false);
        var (rendering, notes) = await FillAsync(found, target, valuesJson, cancellationToken).ConfigureAwait(false);
        if (rendering.Missing.Count > 0)
        {
            throw new MemoryException($"Nothing was drafted: no value for {string.Join(", ", rendering.Missing)}. Pass them in values.");
        }

        return new PreparedDraft(target.Jid, target.Name, rendering.Text, notes);
    }

    private async Task<(TemplateRendering Rendering, string Notes)> FillAsync(
        ResponseTemplate found, ChatRef? target, string? valuesJson, CancellationToken cancellationToken)
    {
        var values = target is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : await ContactValuesAsync(target, cancellationToken).ConfigureAwait(false);
        foreach (var (key, value) in ParseValues(valuesJson))
        {
            values[key] = value;
        }

        var rendering = renderer.Render(found.Body, values);
        var notes = string.Empty;
        if (rendering.Missing.Count == 0 && (found.Voice is not null || found.Category is not null || target is not null))
        {
            try
            {
                var resolved = await selector.SelectAsync(found.Voice, found.Category, target?.Jid, cancellationToken).ConfigureAwait(false);
                var report = checker.Check(rendering.Text, resolved.Rules);
                notes = $"\n\nVoice check: {report.Score}/100"
                    + (report.Findings.Count == 0 ? "." : ": " + string.Join(" ", report.Findings.Select(f => f.Message)));
            }
            catch (MemoryException)
            {
                // No voice to check against is not a reason to refuse the template.
            }
        }

        return (rendering, notes);
    }

    // Every stored field becomes {{contact.field}}; lists give their first item, as for nicknames.
    private async Task<Dictionary<string, string>> ContactValuesAsync(ChatRef target, CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["contact.name"] = target.Name,
            ["contact.first_name"] = target.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? target.Name,
        };
        var now = clock.GetUtcNow();
        foreach (var fact in await contacts.GetFactsAsync(target.Jid, cancellationToken).ConfigureAwait(false))
        {
            if (fact.Sensitive || fact.Expires is { } expires && expires <= now)
            {
                continue;
            }

            using var document = JsonDocument.Parse(fact.ValueJson);
            var root = document.RootElement;
            var text = root.ValueKind switch
            {
                JsonValueKind.String => root.GetString(),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => root.GetRawText(),
                JsonValueKind.Array when root.GetArrayLength() > 0 && root[0].ValueKind == JsonValueKind.String => root[0].GetString(),
                _ => null,
            };
            if (text is not null)
            {
                values["contact." + fact.Field] = text;
            }
        }

        return values;
    }

    private static Dictionary<string, string> ParseValues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new MemoryException("values must be a JSON object such as {\"day\": \"Saturday\"}.");
            }

            return document.RootElement.EnumerateObject().ToDictionary(
                p => p.Name.ToLowerInvariant(),
                p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText(),
                StringComparer.Ordinal);
        }
        catch (JsonException ex)
        {
            throw new MemoryException("values is not valid JSON: " + ex.Message, ex);
        }
    }

    private static string Name(string name)
    {
        var clean = name?.Trim().ToLowerInvariant() ?? string.Empty;
        if (clean.Length is 0 or > 64 || clean.Any(c => !char.IsLetterOrDigit(c) && c is not '-' and not '_'))
        {
            throw new MemoryException("A template name is 1 to 64 letters, digits, dashes or underscores.");
        }

        return clean;
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private async Task<ResponseTemplate> RequireAsync(string name, CancellationToken cancellationToken) =>
        await templates.GetAsync(Name(name), cancellationToken).ConfigureAwait(false)
        ?? throw new MemoryException($"There is no template called {name}. list_templates shows them.");
}
