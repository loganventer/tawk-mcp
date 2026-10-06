using Tawk.Mcp.ResourceAccess.Transcription;

namespace Tawk.Mcp.Tests.ResourceAccess.Transcription;

public class DownsamplerTests
{
    private static float[] Tone(double hertz, int rate, int count) =>
        [.. Enumerable.Range(0, count).Select(i => (float)(0.5 * Math.Sin(2 * Math.PI * hertz * i / rate)))];

    // The loudness of the middle of a signal, away from the ends where the filter has less to work with.
    private static double Level(float[] samples) =>
        Math.Sqrt(samples.Skip(200).Take(samples.Length - 400).Sum(s => (double)s * s) / (samples.Length - 400));

    [Test]
    public void A_voice_keeps_its_pitch_and_its_loudness()
    {
        var heard = Downsampler.ToThird(Tone(440, 48000, 48000));
        var wanted = Tone(440, 16000, 16000);

        Assert.Multiple(() =>
        {
            Assert.That(heard, Has.Length.EqualTo(16000));
            Assert.That(Level(heard), Is.EqualTo(Level(wanted)).Within(0.005));
            Assert.That(Enumerable.Range(200, 15600).Max(i => Math.Abs(heard[i] - wanted[i])), Is.LessThan(0.01), "the same wave, sample for sample");
        });
    }

    [Test]
    public void What_16_kHz_cannot_hold_is_taken_away_and_not_folded_back_in()
    {
        var heard = Downsampler.ToThird(Tone(12000, 48000, 48000));

        Assert.That(Level(heard), Is.LessThan(0.005));
    }

    [Test]
    public void Nothing_in_gives_nothing_out()
    {
        Assert.That(Downsampler.ToThird([]), Is.Empty);
    }
}
