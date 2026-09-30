using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>Checks the draft is no longer than the voice's usual message.</summary>
public sealed class LengthRule : IVoiceRule
{
    public IEnumerable<VoiceFinding> Check(StyleFeatures draft, VoiceRules rules)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.MaxWords is { } max && draft.WordCount > max)
        {
            yield return new VoiceFinding(
                "length",
                draft.WordCount > max * 2 ? FindingSeverity.Error : FindingSeverity.Warning,
                $"Has {draft.WordCount} words; this voice keeps messages to about {max}. Split it or cut it down.");
        }
    }
}
