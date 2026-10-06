using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.Engines.Transcription;

/// <summary>Writes what an agent is told about a transcription job. Every transcript is fenced as untrusted.</summary>
public interface ITranscriptionNoticeFormatter
{
    /// <summary>The answer to a call that queued a job or joined one.</summary>
    string Started(TranscriptionJob job, bool joined);

    /// <summary>A job as it stands: what the channel event carries when it ends, and what get_transcript answers.</summary>
    string Describe(TranscriptionJob job);
}
