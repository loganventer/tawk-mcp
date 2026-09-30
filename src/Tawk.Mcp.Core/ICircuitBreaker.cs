namespace Tawk.Mcp.Core;

/// <summary>Stops connection attempts after repeated failures and lets one trial through after a cooldown.</summary>
public interface ICircuitBreaker
{
    CircuitState State { get; }

    int ConsecutiveFailures { get; }

    /// <summary>When an open circuit next allows a trial, or null when it is not open.</summary>
    DateTimeOffset? RetryAt { get; }

    /// <summary>Returns true when an attempt may go ahead now. In half-open state only one trial is allowed.</summary>
    bool TryBeginAttempt();

    void RecordSuccess();

    void RecordFailure();

    /// <summary>Moves an open circuit to half-open at once, for example when the socket file appears.</summary>
    void ForceTrial();
}
