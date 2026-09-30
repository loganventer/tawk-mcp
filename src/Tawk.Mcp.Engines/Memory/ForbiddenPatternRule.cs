using System.Text.RegularExpressions;
using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>Flags anything the voice never says, such as an em dash, "Dear" or "Kind regards".</summary>
public sealed class ForbiddenPatternRule : IVoiceRule
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);

    public IEnumerable<VoiceFinding> Check(StyleFeatures draft, VoiceRules rules)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(rules);
        foreach (var pattern in rules.ForbiddenPatterns ?? [])
        {
            Match match;
            try
            {
                match = Regex.Match(draft.Text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);
            }
            catch (ArgumentException)
            {
                continue;
            }
            catch (RegexMatchTimeoutException)
            {
                continue;
            }

            if (match.Success)
            {
                yield return new VoiceFinding("forbidden", FindingSeverity.Error, $"Contains \"{match.Value}\", which this voice never uses (pattern {pattern}).");
            }
        }
    }
}
