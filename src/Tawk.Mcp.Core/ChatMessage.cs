namespace Tawk.Mcp.Core;

public sealed record ChatMessage(
    string Id,
    string Chat,
    string? Sender,
    string? SenderName,
    bool FromMe,
    long Ts,
    string Type,
    string? Text,
    string? Status,
    bool Edited,
    bool Deleted,
    bool Forwarded,
    ReplyRef? ReplyTo,
    string? Reactions,
    LinkCard? Link,
    bool MentionsMe);
