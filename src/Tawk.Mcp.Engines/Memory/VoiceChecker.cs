using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>Measures a draft once, runs every composed rule over it, and turns the findings into a score out of 100.</summary>
public sealed class VoiceChecker(IStyleFeatureExtractor extractor, IEnumerable<IVoiceRule> rules) : IVoiceChecker
{
    private readonly IReadOnlyList<IVoiceRule> _rules = rules.ToList();

    public VoiceReport Check(string draft, VoiceRules rules)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(rules);
        var features = extractor.Extract(draft);
        var findings = _rules.SelectMany(rule => rule.Check(features, rules)).ToList();
        var penalty = findings.Sum(f => f.Severity switch
        {
            FindingSeverity.Error => 25,
            FindingSeverity.Warning => 10,
            _ => 3,
        });
        return new VoiceReport(Math.Max(0, 100 - penalty), findings, features);
    }
}
