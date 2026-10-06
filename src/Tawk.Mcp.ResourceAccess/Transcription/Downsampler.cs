namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>
/// Brings sound from 48 kHz down to 16 kHz: a low-pass filter takes away what 16 kHz cannot hold, and then
/// every third sample is kept.
/// </summary>
public static class Downsampler
{
    public const int Factor = 3;

    private const int Taps = 95;

    // Just under half of the new rate, as a share of the old one.
    private const double Cutoff = 7400.0 / 48000.0;

    private static readonly float[] Kernel = Design();

    /// <summary>One sample out for every three in.</summary>
    public static float[] ToThird(IReadOnlyList<float> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var middle = Taps / 2;
        var result = new float[samples.Count / Factor];
        for (var i = 0; i < result.Length; i++)
        {
            var centre = i * Factor;
            var from = Math.Max(0, centre - middle);
            var to = Math.Min(samples.Count - 1, centre + middle);
            var sum = 0f;
            for (var j = from; j <= to; j++)
            {
                sum += samples[j] * Kernel[j - centre + middle];
            }

            result[i] = sum;
        }

        return result;
    }

    // A windowed sinc, scaled so that a steady level comes out as it went in.
    private static float[] Design()
    {
        var kernel = new double[Taps];
        var middle = Taps / 2;
        var total = 0.0;
        for (var i = 0; i < Taps; i++)
        {
            var x = i - middle;
            var sinc = x == 0 ? 2 * Cutoff : Math.Sin(2 * Math.PI * Cutoff * x) / (Math.PI * x);
            var window = 0.54 - (0.46 * Math.Cos(2 * Math.PI * i / (Taps - 1)));
            kernel[i] = sinc * window;
            total += kernel[i];
        }

        return [.. kernel.Select(k => (float)(k / total))];
    }
}
