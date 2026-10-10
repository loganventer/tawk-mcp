namespace Tawk.Mcp.Core;

/// <summary>A chat the user put aside. <see cref="DueAt"/> is epoch seconds, or 0 for "until its person writes".</summary>
public sealed record ReminderItem(ChatRef Chat, long DueAt);
