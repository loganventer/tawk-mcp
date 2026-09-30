using Tawk.Mcp.Engines;

namespace Tawk.Mcp.Tests.Engines;

public class ExponentialBackoffPolicyTests
{
    private static readonly TimeSpan Initial = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan Max = TimeSpan.FromSeconds(30);

    [Test]
    public void The_ceiling_doubles_from_the_initial_delay_up_to_the_cap()
    {
        var backoff = new ExponentialBackoffPolicy(Initial, Max, () => 1);

        var ceilings = Enumerable.Range(0, 9).Select(a => backoff.Ceiling(a).TotalMilliseconds);

        Assert.That(ceilings, Is.EqualTo(new double[] { 500, 1000, 2000, 4000, 8000, 16000, 30000, 30000, 30000 }));
    }

    [TestCase(0.0)]
    [TestCase(0.37)]
    [TestCase(1.0)]
    public void Every_delay_stays_within_bounds(double sample)
    {
        var backoff = new ExponentialBackoffPolicy(Initial, Max, () => sample);

        for (var attempt = 0; attempt < 100; attempt++)
        {
            var delay = backoff.NextDelay(attempt);
            Assert.That(delay, Is.InRange(Initial, backoff.Ceiling(attempt)));
            Assert.That(delay, Is.LessThanOrEqualTo(Max));
        }
    }

    [Test]
    public void Jitter_spreads_delays_across_the_whole_range()
    {
        var samples = new Queue<double>([0.0, 0.5, 1.0]);
        var backoff = new ExponentialBackoffPolicy(Initial, Max, samples.Dequeue);

        var delays = new[] { backoff.NextDelay(3), backoff.NextDelay(3), backoff.NextDelay(3) };

        Assert.That(delays.Select(d => d.TotalMilliseconds), Is.EqualTo(new double[] { 500, 2250, 4000 }));
    }

    [Test]
    public void A_random_source_out_of_range_is_clamped()
    {
        var backoff = new ExponentialBackoffPolicy(Initial, Max, () => 7);

        Assert.That(backoff.NextDelay(50), Is.EqualTo(Max));
    }
}
