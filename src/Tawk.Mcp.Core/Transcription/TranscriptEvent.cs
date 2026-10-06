namespace Tawk.Mcp.Core.Transcription;

/// <summary>A transcription job ended. tawk-mcp raises this itself; it does not come from tawk.</summary>
public sealed record TranscriptEvent(TranscriptionJob Job, string Engine) : TawkEvent;
