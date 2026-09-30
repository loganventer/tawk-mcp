namespace Tawk.Mcp.Engines;

/// <summary>A schedule time as sent to tawk, and how far it was moved from what was asked.</summary>
public sealed record ScheduleNudge(string When, TimeSpan Offset);
