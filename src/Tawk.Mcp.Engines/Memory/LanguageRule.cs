using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>Checks the draft is in a language the voice uses with this audience. "mix" allows either or both.</summary>
public sealed class LanguageRule : IVoiceRule
{
    public IEnumerable<VoiceFinding> Check(StyleFeatures draft, VoiceRules rules)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.Languages is not { Count: > 0 } allowed || draft.Language == "unknown")
        {
            yield break;
        }

        var fits = allowed.Contains(draft.Language, StringComparer.OrdinalIgnoreCase)
            || allowed.Contains("mix", StringComparer.OrdinalIgnoreCase);
        if (!fits)
        {
            yield return new VoiceFinding(
                "language",
                FindingSeverity.Warning,
                $"Reads as {Name(draft.Language)}; with this audience the voice writes in {string.Join(" or ", allowed.Select(Name))}.");
        }
    }

    private static string Name(string code) => code.ToUpperInvariant() switch
    {
        "AF" => "Afrikaans",
        "EN" => "English",
        "MIX" => "a mix of Afrikaans and English",
        _ => code,
    };
}
