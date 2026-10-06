using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.ResourceAccess.Transcription;

namespace Tawk.Mcp.Managers.Transcription;

/// <summary>Carries what one pass reports into the job store, naming the language the pass is for.</summary>
public sealed class PassProgress(ITranscriptionJobStore jobs, string jobId, string language) : IProgress<TranscriptionProgress>
{
    public void Report(TranscriptionProgress value)
    {
        ArgumentNullException.ThrowIfNull(value);
        jobs.Report(jobId, value with { Language = language });
    }
}
