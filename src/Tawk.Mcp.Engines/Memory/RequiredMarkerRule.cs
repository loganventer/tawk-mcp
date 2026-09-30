using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>Suggests a marker the voice usually includes with this audience, such as asb or dankie.</summary>
public sealed class RequiredMarkerRule : IVoiceRule
{
    public IEnumerable<VoiceFinding> Check(StyleFeatures draft, VoiceRules rules)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.MustIncludeAny is { Count: > 0 } markers && !markers.Any(m => WordMatch.Contains(draft.Text, m)))
        {
            yield return new VoiceFinding("marker", FindingSeverity.Info, $"This voice usually includes one of: {string.Join(", ", markers)}.");
        }
    }
}
