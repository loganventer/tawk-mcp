namespace Tawk.Mcp.Core.Transcription;

public enum TranscriptionState
{
    Queued,

    Running,

    /// <summary>Every pass produced text.</summary>
    Done,

    /// <summary>Some passes produced text and some failed.</summary>
    Partial,

    Failed,
}
