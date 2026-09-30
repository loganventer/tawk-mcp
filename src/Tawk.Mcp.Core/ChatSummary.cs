namespace Tawk.Mcp.Core;

public sealed record ChatSummary(
    string Jid,
    string Name,
    bool IsGroup,
    int Unread,
    bool UnreadMention,
    bool Muted,
    bool Pinned,
    bool Archived,
    long LastTs,
    string? Preview);
