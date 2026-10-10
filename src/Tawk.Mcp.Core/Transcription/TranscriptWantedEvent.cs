namespace Tawk.Mcp.Core.Transcription;

/// <summary>
/// tawk asks for the transcript of an older voice note the user looked at. It is treated as one that just
/// arrived: transcribed when the user has automatic transcription on.
/// </summary>
public sealed record TranscriptWantedEvent(ChatRef? Chat, string MessageId) : TawkEvent
{
    /// <summary>The languages the user said this chat's voice notes are spoken in; null when they did not say.</summary>
    public IReadOnlyList<string>? Languages { get; init; }
}
