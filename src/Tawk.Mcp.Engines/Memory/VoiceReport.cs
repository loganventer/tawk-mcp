namespace Tawk.Mcp.Engines.Memory;

/// <summary>A draft's score out of 100 against a voice, with every finding behind it.</summary>
public sealed record VoiceReport(int Score, IReadOnlyList<VoiceFinding> Findings, StyleFeatures Features);
