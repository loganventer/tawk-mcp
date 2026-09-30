namespace Tawk.Mcp.Core;

public sealed record MessagePage(ChatSummary Chat, IReadOnlyList<ChatMessage> Messages, long NextBefore);
