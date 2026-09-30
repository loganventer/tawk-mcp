using System.Globalization;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Engines;
using Tawk.Mcp.Engines.Memory;
using Tawk.Mcp.ResourceAccess;
using Tawk.Mcp.ResourceAccess.Memory;

namespace Tawk.Mcp.Managers.Memory;

public sealed class VoiceManager(
    IVoiceStore voices,
    ICategoryEnsurer categories,
    ITawkChatSource chats,
    IVoiceSelector selector,
    IVoiceChecker checker,
    IVoiceGuideImporter importer,
    IStyleBaselineCalculator baselines,
    IMemoryFormatter formatter,
    IUntrustedTextFence fence,
    IMemoryWriteGuard guard,
    TimeProvider clock) : IVoiceManager, IDraftGuidance
{
    private const string GuideLabel = "a voice guide from tawk-mcp's memory, saved by the user or an agent";

    public async Task<string> ListVoicesAsync(CancellationToken cancellationToken) =>
        formatter.Voices(await voices.ListAsync(cancellationToken).ConfigureAwait(false));

    public async Task<string> GetVoiceAsync(string? voice, string? audience, string? chat, CancellationToken cancellationToken)
    {
        if (voice is not null && audience is null && chat is null)
        {
            var found = await RequireAsync(voice, cancellationToken).ConfigureAwait(false);
            var variants = await voices.ListVariantsAsync(found.Name, cancellationToken).ConfigureAwait(false);
            return fence.Wrap(GuideLabel, formatter.Voice(found, variants));
        }

        var jid = chat is null ? null : (await chats.ResolveAsync(chat, cancellationToken).ConfigureAwait(false)).Jid;
        var resolved = await selector.SelectAsync(voice, audience, jid, cancellationToken).ConfigureAwait(false);
        return fence.Wrap(GuideLabel, formatter.Resolved(resolved));
    }

    public async Task<string> SetVoiceAsync(
        string name, string? description, string? guide, string? rulesJson, bool? isDefault, CancellationToken cancellationToken)
    {
        guard.EnsureWritable();
        var clean = Name(name);
        var existing = await voices.GetAsync(clean, cancellationToken).ConfigureAwait(false);
        if (existing is null && string.IsNullOrWhiteSpace(guide))
        {
            throw new MemoryException("A new voice needs a guide: Markdown describing how the user writes.");
        }

        var voice = new Voice(
            clean,
            description ?? existing?.Description,
            string.IsNullOrWhiteSpace(guide) ? existing!.Guide : guide.Trim(),
            rulesJson is null ? existing?.Rules ?? VoiceRules.None : Rules(rulesJson),
            isDefault ?? existing?.IsDefault ?? (await voices.ListAsync(cancellationToken).ConfigureAwait(false)).Count == 0,
            clock.GetUtcNow());
        await voices.UpsertAsync(voice, cancellationToken).ConfigureAwait(false);
        return $"Voice {clean} saved" + (voice.IsDefault ? " as the default." : ".");
    }

    public async Task<string> SetVariantAsync(
        string voice, string category, string? guide, string? rulesJson, IReadOnlyList<string>? examples, CancellationToken cancellationToken)
    {
        guard.EnsureWritable();
        var parent = await RequireAsync(voice, cancellationToken).ConfigureAwait(false);
        var path = await categories.EnsureAsync(category, cancellationToken).ConfigureAwait(false);
        var existing = (await voices.ListVariantsAsync(parent.Name, cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(v => v.Category == path);
        if (existing is null && string.IsNullOrWhiteSpace(guide))
        {
            throw new MemoryException("A new audience variant needs a guide: how the user writes to this audience.");
        }

        await voices.UpsertVariantAsync(
            new VoiceVariant(
                parent.Name,
                path,
                string.IsNullOrWhiteSpace(guide) ? existing!.Guide : guide.Trim(),
                rulesJson is null ? existing?.Rules ?? VoiceRules.None : Rules(rulesJson),
                examples ?? existing?.Examples ?? [],
                clock.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
        return $"Voice {parent.Name} now has a variant for {path}.";
    }

    public async Task<string> ImportVoiceAsync(
        string name, string markdown, IReadOnlyDictionary<string, string>? sections, string? description, bool isDefault, CancellationToken cancellationToken)
    {
        guard.EnsureWritable();
        var clean = Name(name);
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (heading, category) in sections ?? new Dictionary<string, string>())
        {
            mapping[heading] = await categories.EnsureAsync(category, cancellationToken).ConfigureAwait(false);
        }

        var split = importer.Split(markdown, mapping);
        if (split.BaseGuide.Length == 0)
        {
            throw new MemoryException("The guide is empty once the audience sections are taken out.");
        }

        var now = clock.GetUtcNow();
        var existing = await voices.GetAsync(clean, cancellationToken).ConfigureAwait(false);
        await voices.UpsertAsync(
            new Voice(clean, description ?? existing?.Description, split.BaseGuide, existing?.Rules ?? VoiceRules.None, isDefault || existing?.IsDefault == true, now),
            cancellationToken).ConfigureAwait(false);
        foreach (var section in split.Sections)
        {
            await voices.UpsertVariantAsync(new VoiceVariant(clean, section.Category, section.Guide, VoiceRules.None, [], now), cancellationToken)
                .ConfigureAwait(false);
        }

        var result = $"Imported voice {clean} with {split.Sections.Count} audience variant(s): "
            + (split.Sections.Count == 0 ? "none" : string.Join(", ", split.Sections.Select(s => $"{s.Heading} -> {s.Category}"))) + ".";
        if (split.UnmatchedHeadings.Count > 0)
        {
            result += " No heading matched: " + string.Join(", ", split.UnmatchedHeadings) + ".";
        }

        return result + " Add checkable rules to the voice and each variant with set_voice and set_voice_variant.";
    }

    public async Task<string> ExportVoiceAsync(string name, CancellationToken cancellationToken)
    {
        var voice = await RequireAsync(name, cancellationToken).ConfigureAwait(false);
        var variants = await voices.ListVariantsAsync(voice.Name, cancellationToken).ConfigureAwait(false);
        return fence.Wrap(GuideLabel, formatter.ExportMarkdown(voice, variants));
    }

    public async Task<string> DeleteVoiceAsync(string name, string? category, IUserConfirmation? confirmation, CancellationToken cancellationToken)
    {
        guard.EnsureWritable();
        var voice = await RequireAsync(name, cancellationToken).ConfigureAwait(false);
        if (category is not null)
        {
            var path = CategoryPaths.Normalise(category);
            var variants = await voices.ListVariantsAsync(voice.Name, cancellationToken).ConfigureAwait(false);
            if (variants.All(v => v.Category != path))
            {
                throw new MemoryException($"Voice {voice.Name} has no variant for {path}.");
            }

            await MemoryConfirmations.EnsureAsync(confirmation, $"delete the {path} variant of the voice {voice.Name}", cancellationToken).ConfigureAwait(false);
            await voices.DeleteVariantAsync(voice.Name, path, cancellationToken).ConfigureAwait(false);
            return $"Deleted the {path} variant of voice {voice.Name}.";
        }

        await MemoryConfirmations.EnsureAsync(confirmation, $"delete the voice {voice.Name} and all its audience variants", cancellationToken).ConfigureAwait(false);
        await voices.DeleteAsync(voice.Name, cancellationToken).ConfigureAwait(false);
        return $"Deleted voice {voice.Name}.";
    }

    public async Task<string> CheckVoiceAsync(string draft, string? chat, string? audience, string? voice, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(draft))
        {
            throw new MemoryException("Give the draft text to check.");
        }

        var jid = chat is null ? null : (await chats.ResolveAsync(chat, cancellationToken).ConfigureAwait(false)).Jid;
        var resolved = await selector.SelectAsync(voice, audience, jid, cancellationToken).ConfigureAwait(false);
        var report = checker.Check(draft, resolved.Rules);
        return fence.Wrap(GuideLabel, formatter.Report(report, resolved));
    }

    public async Task<string> LearnVoiceAsync(string voice, string category, IReadOnlyList<string> sourceChats, int messagesPerChat, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceChats);
        guard.EnsureWritable();
        var parent = await RequireAsync(voice, cancellationToken).ConfigureAwait(false);
        var path = await categories.EnsureAsync(category, cancellationToken).ConfigureAwait(false);
        if (sourceChats.Count == 0)
        {
            throw new MemoryException("Name at least one chat whose messages show how the user writes to this audience.");
        }

        var texts = new List<string>();
        foreach (var chat in sourceChats)
        {
            texts.AddRange(await chats.ReadOwnTextsAsync(chat, messagesPerChat, cancellationToken).ConfigureAwait(false));
        }

        var baseline = baselines.Calculate(texts);
        var existing = (await voices.ListVariantsAsync(parent.Name, cancellationToken).ConfigureAwait(false)).FirstOrDefault(v => v.Category == path);
        var rules = (existing?.Rules ?? VoiceRules.None) with { Baseline = baseline };
        await voices.UpsertVariantAsync(
            new VoiceVariant(parent.Name, path, existing?.Guide ?? $"How the user writes to {path}.", rules, existing?.Examples ?? [], clock.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Learnt from {baseline.Samples} of the user's own messages for {parent.Name} / {path}: about {baseline.MeanWords:0.#} words, {baseline.LowercaseShare:P0} start in lowercase, {baseline.EmojiPerMessage:0.##} emoji a message, {baseline.LaughShare:P0} laugh. Only these numbers were kept, not the messages.");
    }

    public async Task<string> ForChatAsync(string chat, CancellationToken cancellationToken)
    {
        if ((await voices.ListAsync(cancellationToken).ConfigureAwait(false)).Count == 0)
        {
            return string.Empty;
        }

        var jid = (await chats.ResolveAsync(chat, cancellationToken).ConfigureAwait(false)).Jid;
        ResolvedVoice resolved;
        try
        {
            resolved = await selector.SelectAsync(null, null, jid, cancellationToken).ConfigureAwait(false);
        }
        catch (MemoryException)
        {
            return string.Empty;
        }

        return "How the user writes to this person, from tawk-mcp's memory. Follow it, then call check_voice with the draft "
            + "and chat before showing it, and fix anything it flags. get_contact has more about the person.\n"
            + fence.Wrap(GuideLabel, formatter.Resolved(resolved));
    }

    private static string Name(string name)
    {
        var clean = name?.Trim() ?? string.Empty;
        if (clean.Length is 0 or > 64 || clean.Any(c => !char.IsLetterOrDigit(c) && c is not '-' and not '_'))
        {
            throw new MemoryException("A voice name is 1 to 64 letters, digits, dashes or underscores.");
        }

        return clean.ToLowerInvariant();
    }

    private static VoiceRules Rules(string json) =>
        RulesJson.Read(json, out var problem) ?? throw new MemoryException("Those rules are not valid: " + problem);

    private async Task<Voice> RequireAsync(string name, CancellationToken cancellationToken) =>
        await voices.GetAsync(Name(name), cancellationToken).ConfigureAwait(false)
        ?? throw new MemoryException($"There is no voice called {name}. list_voices shows them.");
}
