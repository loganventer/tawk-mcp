using Tawk.Mcp.Core;
using Tawk.Mcp.Engines;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Engines;

public class CircuitBreakerTests
{
    private readonly ManualTimeProvider _clock = new();

    private CircuitBreaker Breaker() => new(3, TimeSpan.FromSeconds(60), _clock);

    private static void Fail(CircuitBreaker breaker, int times)
    {
        for (var i = 0; i < times; i++)
        {
            Assert.That(breaker.TryBeginAttempt(), Is.True);
            breaker.RecordFailure();
        }
    }

    [Test]
    public void Starts_closed_and_allows_attempts()
    {
        var breaker = Breaker();

        Assert.Multiple(() =>
        {
            Assert.That(breaker.State, Is.EqualTo(CircuitState.Closed));
            Assert.That(breaker.TryBeginAttempt(), Is.True);
            Assert.That(breaker.RetryAt, Is.Null);
        });
    }

    [Test]
    public void Stays_closed_below_the_threshold()
    {
        var breaker = Breaker();

        Fail(breaker, 2);

        Assert.That(breaker.State, Is.EqualTo(CircuitState.Closed));
    }

    [Test]
    public void Opens_after_the_threshold_and_refuses_attempts_during_the_cooldown()
    {
        var breaker = Breaker();
        Fail(breaker, 3);
        _clock.Advance(TimeSpan.FromSeconds(59));

        Assert.Multiple(() =>
        {
            Assert.That(breaker.State, Is.EqualTo(CircuitState.Open));
            Assert.That(breaker.TryBeginAttempt(), Is.False);
            Assert.That(breaker.RetryAt, Is.EqualTo(_clock.GetUtcNow() + TimeSpan.FromSeconds(1)));
            Assert.That(breaker.ConsecutiveFailures, Is.EqualTo(3));
        });
    }

    [Test]
    public void Goes_half_open_after_the_cooldown_and_allows_one_trial()
    {
        var breaker = Breaker();
        Fail(breaker, 3);
        _clock.Advance(TimeSpan.FromSeconds(60));

        Assert.Multiple(() =>
        {
            Assert.That(breaker.TryBeginAttempt(), Is.True);
            Assert.That(breaker.State, Is.EqualTo(CircuitState.HalfOpen));
            Assert.That(breaker.TryBeginAttempt(), Is.False);
        });
    }

    [Test]
    public void A_successful_trial_closes_it()
    {
        var breaker = Breaker();
        Fail(breaker, 3);
        _clock.Advance(TimeSpan.FromSeconds(60));
        breaker.TryBeginAttempt();

        breaker.RecordSuccess();

        Assert.Multiple(() =>
        {
            Assert.That(breaker.State, Is.EqualTo(CircuitState.Closed));
            Assert.That(breaker.ConsecutiveFailures, Is.Zero);
        });
    }

    [Test]
    public void A_failed_trial_opens_it_again_for_a_new_cooldown()
    {
        var breaker = Breaker();
        Fail(breaker, 3);
        _clock.Advance(TimeSpan.FromSeconds(60));
        breaker.TryBeginAttempt();

        breaker.RecordFailure();

        Assert.Multiple(() =>
        {
            Assert.That(breaker.State, Is.EqualTo(CircuitState.Open));
            Assert.That(breaker.RetryAt, Is.EqualTo(_clock.GetUtcNow() + TimeSpan.FromSeconds(60)));
        });
    }

    [Test]
    public void The_socket_appearing_forces_an_immediate_trial()
    {
        var breaker = Breaker();
        Fail(breaker, 3);

        breaker.ForceTrial();

        Assert.Multiple(() =>
        {
            Assert.That(breaker.State, Is.EqualTo(CircuitState.HalfOpen));
            Assert.That(breaker.TryBeginAttempt(), Is.True);
        });
    }

    [Test]
    public void Forcing_a_trial_on_a_closed_circuit_changes_nothing()
    {
        var breaker = Breaker();

        breaker.ForceTrial();

        Assert.That(breaker.State, Is.EqualTo(CircuitState.Closed));
    }
}
