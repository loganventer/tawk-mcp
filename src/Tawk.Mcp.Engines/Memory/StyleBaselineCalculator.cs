using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

public sealed class StyleBaselineCalculator(IStyleFeatureExtractor extractor) : IStyleBaselineCalculator
{
    public StyleBaseline Calculate(IReadOnlyList<string> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var features = messages.Select(extractor.Extract).Where(f => f.WordCount > 0).ToList();
        if (features.Count == 0)
        {
            return new StyleBaseline(0, 0, 0, 0, 0, 0, 0);
        }

        var words = features.Select(f => (double)f.WordCount).ToList();
        var mean = words.Average();
        var spread = Math.Sqrt(words.Sum(w => (w - mean) * (w - mean)) / words.Count);
        return new StyleBaseline(
            features.Count,
            mean,
            spread,
            features.Count(f => f.StartsLowercase) / (double)features.Count,
            features.Average(f => f.Emoji.Count),
            features.Count(f => f.EllipsisCount > 0) / (double)features.Count,
            features.Count(f => f.HasLaugh) / (double)features.Count);
    }
}
