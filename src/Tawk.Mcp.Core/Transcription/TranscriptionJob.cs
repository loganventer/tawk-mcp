namespace Tawk.Mcp.Core.Transcription;

/// <summary>A transcription asked for, with one pass for each language. <see cref="Failure"/> is set when no pass could start.</summary>
public sealed record TranscriptionJob(
    string Id,
    TranscriptionRequest Request,
    TranscriptionState State,
    IReadOnlyList<TranscriptionPass> Passes,
    DateTimeOffset CreatedAt,
    ChatRef? Chat = null,
    string? Failure = null,
    DateTimeOffset? EndedAt = null)
{
    public bool Ended => State is TranscriptionState.Done or TranscriptionState.Partial or TranscriptionState.Failed;
}
