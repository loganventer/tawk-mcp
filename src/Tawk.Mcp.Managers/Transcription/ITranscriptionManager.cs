namespace Tawk.Mcp.Managers.Transcription;

public interface ITranscriptionManager
{
    /// <summary>
    /// Queues a transcription and returns at once with what to tell the agent. The text follows later
    /// through every event sink. Throws <see cref="Core.Transcription.TranscriptionException"/> when the
    /// choices are not allowed.
    /// </summary>
    Task<string> StartAsync(
        string messageId,
        string? account,
        IReadOnlyList<string>? languages,
        string? task,
        string? model,
        string? prompt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Queues a transcription of a voice note that just arrived, with the user's defaults, when the user
    /// turned automatic transcription on. Returns whether it did.
    /// </summary>
    Task<bool> StartAutomaticAsync(string messageId, string? account, CancellationToken cancellationToken);

    /// <summary>A job as it stands, for a client without channels.</summary>
    string Read(string jobId);

    /// <summary>How far a job is, or every job that is queued or running when no id is given. It carries no transcript.</summary>
    string Progress(string? jobId);
}
