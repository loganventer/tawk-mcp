using Tawk.Mcp.Core;

namespace Tawk.Mcp.Engines;

/// <summary>
/// Doubles from the initial delay up to the cap, with full jitter: each wait is drawn evenly between the
/// initial delay and the current ceiling, so clients do not retry in step. The initial delay is a floor.
/// </summary>
public sealed class ExponentialBackoffPolicy : IBackoffPolicy
{
    private readonly TimeSpan _initial;
    private readonly TimeSpan _max;
    private readonly Func<double> _random;

    public ExponentialBackoffPolicy(TimeSpan initial, TimeSpan max, Func<double> random)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(initial, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(max, initial);
        ArgumentNullException.ThrowIfNull(random);
        _initial = initial;
        _max = max;
        _random = random;
    }

    public TimeSpan Ceiling(int attempt)
    {
        var exponent = Math.Clamp(attempt, 0, 30);
        var ticks = Math.Min(_initial.Ticks * Math.Pow(2, exponent), _max.Ticks);
        return TimeSpan.FromTicks((long)ticks);
    }

    public TimeSpan NextDelay(int attempt)
    {
        var ceiling = Ceiling(attempt);
        var sample = Math.Clamp(_random(), 0d, 1d);
        var ticks = _initial.Ticks + ((ceiling.Ticks - _initial.Ticks) * sample);
        return TimeSpan.FromTicks((long)ticks);
    }
}
