namespace Tawk.Mcp.Core.Transcription;

/// <summary>
/// One pass's text. <see cref="DetectedLanguage"/> is what the engine heard when it was left to detect,
/// and <see cref="DurationS"/> the recording's length where the engine reports it.
/// </summary>
public sealed record Transcript(string Text, string? DetectedLanguage, string? Model, double? DurationS);
