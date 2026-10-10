namespace Tawk.Mcp.Core;

/// <summary>The one-to-one chats where the user's last message has gone unanswered for <see cref="Days"/> days or more.</summary>
public sealed record AwaitingReplies(int Days, IReadOnlyList<ChatSummary> Chats);
