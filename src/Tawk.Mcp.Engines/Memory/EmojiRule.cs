using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>Checks how many emoji there are and whether they are ones the voice uses.</summary>
public sealed class EmojiRule : IVoiceRule
{
    public IEnumerable<VoiceFinding> Check(StyleFeatures draft, VoiceRules rules)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.MaxEmoji is { } max && draft.Emoji.Count > max)
        {
            yield return new VoiceFinding(
                "emoji",
                draft.Emoji.Count > max + 1 ? FindingSeverity.Error : FindingSeverity.Warning,
                $"Has {draft.Emoji.Count} emoji; this voice uses at most {max}.");
        }

        if (rules.AllowedEmoji is { Count: > 0 } allowed)
        {
            var strangers = draft.Emoji.Where(e => !allowed.Contains(e, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal).ToList();
            if (strangers.Count > 0)
            {
                yield return new VoiceFinding("emoji", FindingSeverity.Info, $"Uses {string.Join(' ', strangers)}, which this voice does not usually use.");
            }
        }
    }
}
