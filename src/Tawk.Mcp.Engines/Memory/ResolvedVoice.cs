using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>The voice to write in for one audience: the base guide, the variant that matched (if any), and the rules in force.</summary>
public sealed record ResolvedVoice(Voice Voice, VoiceVariant? Variant, VoiceRules Rules);
