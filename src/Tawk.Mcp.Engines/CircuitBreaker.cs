using Tawk.Mcp.Core;

namespace Tawk.Mcp.Engines;

public sealed class CircuitBreaker : ICircuitBreaker
{
    private readonly Lock _gate = new();
    private readonly int _threshold;
    private readonly TimeSpan _cooldown;
    private readonly TimeProvider _clock;
    private CircuitState _state = CircuitState.Closed;
    private int _failures;
    private DateTimeOffset _openedAt;
    private bool _trialInFlight;

    public CircuitBreaker(int threshold, TimeSpan cooldown, TimeProvider clock)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(threshold, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(cooldown, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(clock);
        _threshold = threshold;
        _cooldown = cooldown;
        _clock = clock;
    }

    public CircuitState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public int ConsecutiveFailures
    {
        get
        {
            lock (_gate)
            {
                return _failures;
            }
        }
    }

    public DateTimeOffset? RetryAt
    {
        get
        {
            lock (_gate)
            {
                return _state == CircuitState.Open ? _openedAt + _cooldown : null;
            }
        }
    }

    public bool TryBeginAttempt()
    {
        lock (_gate)
        {
            switch (_state)
            {
                case CircuitState.Closed:
                    return true;
                case CircuitState.Open when _clock.GetUtcNow() >= _openedAt + _cooldown:
                    _state = CircuitState.HalfOpen;
                    _trialInFlight = true;
                    return true;
                case CircuitState.HalfOpen when !_trialInFlight:
                    _trialInFlight = true;
                    return true;
                default:
                    return false;
            }
        }
    }

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _state = CircuitState.Closed;
            _failures = 0;
            _trialInFlight = false;
        }
    }

    public void RecordFailure()
    {
        lock (_gate)
        {
            _failures++;
            _trialInFlight = false;
            if (_state == CircuitState.HalfOpen || _failures >= _threshold)
            {
                _state = CircuitState.Open;
                _openedAt = _clock.GetUtcNow();
            }
        }
    }

    public void ForceTrial()
    {
        lock (_gate)
        {
            if (_state == CircuitState.Open)
            {
                _state = CircuitState.HalfOpen;
                _trialInFlight = false;
            }
        }
    }
}
