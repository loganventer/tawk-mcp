using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>
/// The queue of transcription jobs and the recent ones that ended. Kept in memory only, a bounded number
/// for a bounded time: tawk-mcp stores no messages.
/// </summary>
public interface ITranscriptionJobStore
{
    /// <summary>
    /// Queues a job for the request, or returns the one already queued or running for the same request.
    /// </summary>
    TranscriptionJob Add(TranscriptionRequest request, out bool joined);

    /// <summary>Waits for the next queued job and marks it running.</summary>
    Task<TranscriptionJob> TakeAsync(CancellationToken cancellationToken);

    /// <summary>Records a job's progress or its end.</summary>
    void Save(TranscriptionJob job);

    TranscriptionJob? Find(string id);
}
