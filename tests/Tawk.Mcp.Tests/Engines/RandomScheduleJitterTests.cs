using Tawk.Mcp.Engines;

namespace Tawk.Mcp.Tests.Engines;

public class RandomScheduleJitterTests
{
    [Test]
    public void Appends_the_offset_in_whole_seconds_and_keeps_the_milliseconds()
    {
        var later = new RandomScheduleJitter(TimeSpan.FromSeconds(60), () => 0.8).Nudge("18:00");
        var earlier = new RandomScheduleJitter(TimeSpan.FromSeconds(60), () => 0.1).Nudge("+5m");

        Assert.Multiple(() =>
        {
            Assert.That(later.When, Is.EqualTo("18:00 +36s"));
            Assert.That(later.Offset, Is.EqualTo(TimeSpan.FromMilliseconds(36_000)));
            Assert.That(earlier.When, Is.EqualTo("+5m -48s"));
            Assert.That(earlier.Offset, Is.EqualTo(TimeSpan.FromMilliseconds(-48_000)));
        });
    }

    [Test]
    public void Stays_within_the_maximum_either_way()
    {
        var random = new Random(7);
        var jitter = new RandomScheduleJitter(TimeSpan.FromSeconds(60), random.NextDouble);

        var offsets = Enumerable.Range(0, 1000).Select(_ => jitter.Nudge("18:00").Offset).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(offsets, Has.All.InRange(TimeSpan.FromSeconds(-60), TimeSpan.FromSeconds(60)));
            Assert.That(offsets.Any(o => o < TimeSpan.Zero) && offsets.Any(o => o > TimeSpan.Zero), Is.True);
            Assert.That(offsets.Any(o => o.Milliseconds != 0), Is.True, "offsets carry milliseconds");
        });
    }

    [Test]
    public void Leaves_the_time_alone_when_off_or_under_half_a_second()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new RandomScheduleJitter(TimeSpan.Zero, () => 0.9).Nudge("18:00").When, Is.EqualTo("18:00"));
            Assert.That(new RandomScheduleJitter(TimeSpan.FromSeconds(60), () => 0.5).Nudge("18:00").When, Is.EqualTo("18:00"));
        });
    }
}
