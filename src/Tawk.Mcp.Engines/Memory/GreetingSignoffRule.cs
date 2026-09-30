using System.Text.RegularExpressions;
using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>Checks the opening greeting and the closing sign-off against the voice's habits.</summary>
public sealed partial class GreetingSignoffRule : IVoiceRule
{
    public IEnumerable<VoiceFinding> Check(StyleFeatures draft, VoiceRules rules)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(rules);
        var greets = Greeting().IsMatch(draft.FirstLine);
        var signs = Signoff().IsMatch(draft.LastLine);
        foreach (var finding in Policy("greeting", rules.Greeting, greets, "Opens with a greeting", "Has no greeting"))
        {
            yield return finding;
        }

        foreach (var finding in Policy("signoff", rules.Signoff, signs, "Ends with a sign-off", "Has no sign-off"))
        {
            yield return finding;
        }
    }

    private static IEnumerable<VoiceFinding> Policy(string rule, string? policy, bool present, string presentText, string missingText)
    {
        if (string.Equals(policy, "none", StringComparison.OrdinalIgnoreCase) && present)
        {
            yield return new VoiceFinding(rule, FindingSeverity.Warning, presentText + "; this voice usually jumps straight in without one.");
        }
        else if (string.Equals(policy, "required", StringComparison.OrdinalIgnoreCase) && !present)
        {
            yield return new VoiceFinding(rule, FindingSeverity.Warning, missingText + "; this voice uses one with this audience.");
        }
    }

    [GeneratedRegex(@"^\s*(hi|hey|hello|hallo|dear|good (morning|afternoon|evening)|goeie(m[oô]re|middag|naand)|m[oô]re|howzit|yo)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Greeting();

    [GeneratedRegex(@"(kind regards|regards|best wishes|sincerely|cheers|groete|vriendelike groete|liefde|love you|lief vir (jou|ma|pa)|sien (jou|dan|netnou))[\s.!,]*\S{0,3}\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Signoff();
}
