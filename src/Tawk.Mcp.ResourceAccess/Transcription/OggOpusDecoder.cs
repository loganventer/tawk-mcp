using Concentus;
using Concentus.Structs;
using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>
/// WhatsApp voice notes are Opus in an Ogg file, decoded in managed code, so no other program is needed.
/// Opus is decoded at its own 48 kHz and brought down to 16 kHz here: asked for 16 kHz directly, the decoder
/// gives silence for the wider modes that many phones record in.
/// </summary>
public sealed class OggOpusDecoder : IAudioDecoder
{
    public const int SampleRate = 16000;

    private const int DecodeRate = SampleRate * Downsampler.Factor;

    // The longest an Opus packet can be is 120 ms.
    private const int LongestPacket = DecodeRate * 120 / 1000;

    // A voice note is given up on when more than one packet in this many cannot be read.
    private const int SkippedShare = 10;

    public float[] Decode(string path, int maxSeconds)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Span<byte> magic = stackalloc byte[4];
            if (file.Read(magic) != 4 || !magic.SequenceEqual("OggS"u8))
            {
                throw new TranscriptionException("unsupported format: only Ogg Opus voice notes can be transcribed in process");
            }

            file.Position = 0;
            return Decode(file, (long)maxSeconds * DecodeRate);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new TranscriptionException("the audio file could not be read", ex);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or InvalidOperationException or OpusException)
        {
            throw new TranscriptionException("the voice note could not be decoded", ex);
        }
    }

    private static float[] Decode(Stream file, long limit)
    {
        var decoder = OpusCodecFactory.CreateDecoder(DecodeRate, 1);
        var frame = new short[LongestPacket];
        var samples = new List<float>(DecodeRate * 30);
        var described = false;
        var decoded = 0;
        var skipped = 0;
        foreach (var packet in OggPackets.Read(file))
        {
            // The stream opens with two packets that describe it and hold no sound.
            if (Starts(packet, "OpusHead"u8) || Starts(packet, "OpusTags"u8))
            {
                described = true;
                continue;
            }

            if (!described)
            {
                throw new TranscriptionException("unsupported format: only Ogg Opus voice notes can be transcribed in process");
            }

            if (packet.Length == 0)
            {
                continue;
            }

            int count;
            try
            {
                count = decoder.Decode(packet, frame, frame.Length, false);
                decoded++;
            }
            catch (Exception ex) when (ex is OpusException or ArgumentException or InvalidOperationException or IndexOutOfRangeException)
            {
                // The decoder refuses some packets that phones write. One of them is left out as silence of
                // its own length and the decoder starts afresh, so a fraction of a second is lost, not the note.
                skipped++;
                decoder.ResetState();
                count = Length(packet);
                Array.Clear(frame, 0, count);
            }

            for (var i = 0; i < count; i++)
            {
                samples.Add(frame[i] / 32768f);
            }

            if (samples.Count > limit)
            {
                throw new TranscriptionException("too long");
            }
        }

        if (!described || skipped * SkippedShare > decoded + skipped)
        {
            throw new TranscriptionException("the voice note could not be decoded");
        }

        return decoded == 0 ? throw new TranscriptionException("the voice note holds no sound") : Downsampler.ToThird(samples);
    }

    private static bool Starts(byte[] packet, ReadOnlySpan<byte> with) => packet.AsSpan().StartsWith(with);

    // How many samples a packet stands for, by what it says of itself, or none when it does not say.
    private static int Length(byte[] packet)
    {
        try
        {
            return Math.Clamp(OpusPacketInfo.GetNumFrames(packet) * OpusPacketInfo.GetNumSamplesPerFrame(packet, DecodeRate), 0, LongestPacket);
        }
        catch (Exception ex) when (ex is OpusException or ArgumentException or IndexOutOfRangeException)
        {
            return 0;
        }
    }
}
