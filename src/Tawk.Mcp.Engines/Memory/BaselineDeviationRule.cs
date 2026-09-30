using System.Globalization;
using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>
/// Compares the draft with averages learned from the user's own messages. A single message is too short to judge
/// style reliably, so these findings are hints, never errors.
/// </summary>
public sealed class BaselineDeviationRule : IVoiceRule
{
    private const int MinimumSamples = 10;

    public IEnumerable<VoiceFinding> Check(StyleFeatures draft, VoiceRules rules)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.Baseline is not { Samples: >= MinimumSamples } baseline)
        {
            yield break;
        }

        var spread = Math.Max(baseline.StdDevWords, 1);
        if ((draft.WordCount - baseline.MeanWords) / spread > 2)
        {
            yield return new VoiceFinding(
                "baseline",
                FindingSeverity.Info,
                string.Create(CultureInfo.InvariantCulture, $"Longer than usual: {draft.WordCount} words against a usual {baseline.MeanWords:0.#}."));
        }

        if (baseline.LowercaseShare >= 0.7 && draft.StartsUppercase)
        {
            yield return new VoiceFinding(
                "baseline",
                FindingSeverity.Info,
                string.Create(CultureInfo.InvariantCulture, $"{baseline.LowercaseShare:P0} of the user's messages here start in lowercase."));
        }

        if (baseline.EmojiPerMessage < 0.25 && draft.Emoji.Count >= 2)
        {
            yield return new VoiceFinding("baseline", FindingSeverity.Info, "More emoji than usual; the user rarely uses more than one here.");
        }
    }
}
