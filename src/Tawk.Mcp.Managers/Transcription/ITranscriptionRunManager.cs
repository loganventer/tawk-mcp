namespace Tawk.Mcp.Managers.Transcription;

public interface ITranscriptionRunManager
{
    /// <summary>
    /// Waits for the next queued job, runs it to its end and hands the outcome to every event sink. It does
    /// not throw for a job that fails.
    /// </summary>
    Task RunNextAsync(CancellationToken cancellationToken);
}
