namespace Tawk.Mcp.Core;

public sealed record ScheduledMessage(string Id, long DueAt, bool Edited = false, string? Text = null, bool Disclaimer = false);
