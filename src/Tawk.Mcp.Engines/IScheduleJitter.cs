namespace Tawk.Mcp.Engines;

/// <summary>Moves a schedule time by a small random amount, so scheduled messages do not all land on the exact minute.</summary>
public interface IScheduleJitter
{
    ScheduleNudge Nudge(string requested);
}
