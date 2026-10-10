namespace Tawk.Mcp.Core;

public sealed record MessageEvent(ChatRef Chat, ChatMessage Message) : TawkEvent
{
    /// <summary>False when the user switched transcribing off for this chat in tawk, so its voice notes are left alone.</summary>
    public bool Transcribe { get; init; } = true;

    /// <summary>The languages the user said this chat's voice notes are spoken in; null when they did not say.</summary>
    public IReadOnlyList<string>? Languages { get; init; }
}
