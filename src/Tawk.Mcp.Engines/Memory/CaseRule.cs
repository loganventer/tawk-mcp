using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>Checks how the draft starts: lowercase for a lowercase voice, a capital for a sentence-case one.</summary>
public sealed class CaseRule : IVoiceRule
{
    public IEnumerable<VoiceFinding> Check(StyleFeatures draft, VoiceRules rules)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(rules);
        if (string.Equals(rules.Case, "lower", StringComparison.OrdinalIgnoreCase) && draft.StartsUppercase)
        {
            yield return new VoiceFinding("case", FindingSeverity.Warning, "Starts with a capital; this voice usually writes in lowercase.");
        }
        else if (string.Equals(rules.Case, "sentence", StringComparison.OrdinalIgnoreCase) && draft.StartsLowercase)
        {
            yield return new VoiceFinding("case", FindingSeverity.Warning, "Starts in lowercase; this voice starts sentences with a capital here.");
        }
    }
}
