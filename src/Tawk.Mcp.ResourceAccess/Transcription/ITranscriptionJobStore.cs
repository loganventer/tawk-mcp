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

    /// <summary>Notes the step a job is on. It is kept beside the job and dropped when the job ends.</summary>
    void Report(string id, TranscriptionProgress progress);

    /// <summary>The step a job that has not ended is on, or null when none was reported.</summary>
    TranscriptionProgress? Progress(string id);

    /// <summary>The jobs that are queued or running, oldest first.</summary>
    IReadOnlyList<TranscriptionJob> Active();
}
