namespace Tawk.Mcp.Core;

public sealed record CatchUpPlan(string Instructions, IReadOnlyList<ChatSummary> Chats);
