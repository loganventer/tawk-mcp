namespace Tawk.Mcp.Core.Transcription;

/// <summary>
/// tawk asks for the transcript of an older voice note the user looked at. It is treated as one that just
/// arrived: transcribed when the user has automatic transcription on.
/// </summary>
public sealed record TranscriptWantedEvent(ChatRef? Chat, string MessageId) : TawkEvent;
