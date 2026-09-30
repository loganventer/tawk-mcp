namespace Tawk.Mcp.Core;

public sealed record ScheduledItem(string Id, string Chat, string Text, long DueAt);
