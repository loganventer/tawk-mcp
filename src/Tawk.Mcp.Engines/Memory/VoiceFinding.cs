namespace Tawk.Mcp.Engines.Memory;

/// <summary>One way a draft departs from a voice, and how much it matters.</summary>
public sealed record VoiceFinding(string Rule, FindingSeverity Severity, string Message);
