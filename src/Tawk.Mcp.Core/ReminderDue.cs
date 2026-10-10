namespace Tawk.Mcp.Core;

/// <summary>When a chat that was put aside comes back: epoch seconds, or 0 for "until its person writes".</summary>
public sealed record ReminderDue(long DueAt);
