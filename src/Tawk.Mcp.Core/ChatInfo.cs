namespace Tawk.Mcp.Core;

public sealed record ChatInfo(ChatSummary Chat, string? About, IReadOnlyList<GroupMember>? Members);
