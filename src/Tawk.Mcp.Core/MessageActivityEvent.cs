namespace Tawk.Mcp.Core;

/// <summary>
/// Something happened to a message: a reaction, an edit, a delete, or a scheduled send. <see cref="Who"/> is
/// who did it where someone did, <see cref="Emoji"/> the reaction (empty when taken back), and
/// <see cref="Message"/> the message as it now reads after an edit. <see cref="At"/> is in Unix seconds.
/// </summary>
public sealed record MessageActivityEvent(
    ActivityKind Kind, ChatRef Chat, string MessageId, ReaderRef? Who, string? Emoji, ChatMessage? Message, long At) : TawkEvent;
