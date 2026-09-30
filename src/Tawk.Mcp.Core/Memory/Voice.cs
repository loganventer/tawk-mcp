namespace Tawk.Mcp.Core.Memory;

/// <summary>How someone writes: a guide for the model and rules a draft can be checked against.</summary>
public sealed record Voice(string Name, string? Description, string Guide, VoiceRules Rules, bool IsDefault, DateTimeOffset Updated);
