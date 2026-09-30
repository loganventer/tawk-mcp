using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>One style check. Returns no findings when its rule is not set, and never throws for a valid draft.</summary>
public interface IVoiceRule
{
    IEnumerable<VoiceFinding> Check(StyleFeatures draft, VoiceRules rules);
}
