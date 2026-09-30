namespace Tawk.Mcp.Tests.Fakes;

/// <summary>A clock that only moves when told to. Local time is UTC so formatted times are predictable.</summary>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public ManualTimeProvider()
        : this(new DateTimeOffset(2026, 9, 30, 18, 0, 0, TimeSpan.Zero))
    {
    }

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
