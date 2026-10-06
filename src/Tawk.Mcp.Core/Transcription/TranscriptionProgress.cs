namespace Tawk.Mcp.Core.Transcription;

/// <summary>
/// How far a running transcription is. <see cref="Percent"/> is the share of the voice note heard so far,
/// <see cref="Bytes"/> is how much of a model has been downloaded, and <see cref="Language"/> names the pass.
/// </summary>
public sealed record TranscriptionProgress(TranscriptionStage Stage, int? Percent = null, long? Bytes = null, string? Language = null);
