using Concentus;
using Concentus.Oggfile;
using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>
/// WhatsApp voice notes are Opus in an Ogg file. Opus decodes straight to 16 kHz mono, in managed code, so
/// no other program and no resampling is needed.
/// </summary>
public sealed class OggOpusDecoder : IAudioDecoder
{
    public const int SampleRate = 16000;

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
            var decoder = OpusCodecFactory.CreateDecoder(SampleRate, 1);
            var ogg = new OpusOggReadStream(decoder, file);
            var limit = (long)maxSeconds * SampleRate;
            var samples = new List<float>(SampleRate * 30);
            while (ogg.HasNextPacket)
            {
                var packet = ogg.DecodeNextPacket();
                if (packet is null)
                {
                    continue;
                }

                foreach (var sample in packet)
                {
                    samples.Add(sample / 32768f);
                }

                if (samples.Count > limit)
                {
                    throw new TranscriptionException("too long");
                }
            }

            return samples.Count == 0 ? throw new TranscriptionException("the voice note holds no sound") : [.. samples];
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
}
