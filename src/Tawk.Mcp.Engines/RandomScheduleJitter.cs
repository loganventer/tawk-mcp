using System.Globalization;

namespace Tawk.Mcp.Engines;

/// <summary>
/// Picks an offset between minus and plus the maximum, to the millisecond, and appends it to tawk's time as whole
/// seconds ("18:00 +37s"), because tawk schedules to the second. tawk never lets the offset move a message into the past.
/// </summary>
public sealed class RandomScheduleJitter(TimeSpan maximum, Func<double> nextUnit) : IScheduleJitter
{
    public ScheduleNudge Nudge(string requested)
    {
        ArgumentNullException.ThrowIfNull(requested);
        if (maximum <= TimeSpan.Zero || string.IsNullOrWhiteSpace(requested))
        {
            return new ScheduleNudge(requested, TimeSpan.Zero);
        }

        var offset = TimeSpan.FromMilliseconds(Math.Round((nextUnit() * 2 - 1) * maximum.TotalMilliseconds));
        var seconds = (long)Math.Round(offset.TotalSeconds, MidpointRounding.AwayFromZero);
        if (seconds == 0)
        {
            return new ScheduleNudge(requested, offset);
        }

        var sign = seconds > 0 ? "+" : "-";
        return new ScheduleNudge(
            string.Create(CultureInfo.InvariantCulture, $"{requested.Trim()} {sign}{Math.Abs(seconds)}s"),
            offset);
    }
}
