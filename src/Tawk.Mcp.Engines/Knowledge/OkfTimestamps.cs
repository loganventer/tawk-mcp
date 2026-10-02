using System.Globalization;

namespace Tawk.Mcp.Engines.Knowledge;

/// <summary>OKF timestamps: ISO 8601 with an explicit offset. Written in UTC, to the millisecond.</summary>
internal static class OkfTimestamps
{
    public static string Format(DateTimeOffset time) =>
        time.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public static DateTimeOffset? Parse(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time)
            ? time
            : null;
}
