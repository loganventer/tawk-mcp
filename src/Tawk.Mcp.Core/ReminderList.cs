namespace Tawk.Mcp.Core;

/// <summary>The chats the user put aside, among those this agent may see.</summary>
public sealed record ReminderList(IReadOnlyList<ReminderItem> Reminders);
