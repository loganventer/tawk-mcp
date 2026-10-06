namespace Tawk.Mcp.Core.Transcription;

/// <summary>One language of a job: waiting, or done with its transcript, or failed with a reason safe to show.</summary>
public sealed record TranscriptionPass(string Language, Transcript? Transcript = null, string? Failure = null)
{
    public bool Ended => Transcript is not null || Failure is not null;
}
