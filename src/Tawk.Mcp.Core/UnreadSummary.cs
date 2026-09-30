namespace Tawk.Mcp.Core;

public sealed record UnreadSummary(int Total, int Mentions, IReadOnlyList<ChatSummary> Chats);
