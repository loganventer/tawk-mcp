using System.Buffers.Binary;
using System.Text;
using Concentus;
using Concentus.Enums;

namespace Tawk.Mcp.Tests.Fakes;

/// <summary>Builds small Ogg Opus files in memory, the way a phone lays a voice note out.</summary>
public static class OggFiles
{
    public const int SampleRate = 16000;

    /// <summary>20 ms of sound in each packet.</summary>
    public const int PacketSamples = SampleRate / 50;

    /// <summary>The two packets every Opus stream opens with.</summary>
    public static IReadOnlyList<byte[]> Opening()
    {
        var head = new byte[19];
        "OpusHead"u8.CopyTo(head);
        head[8] = 1;
        head[9] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(10), 312);
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(12), SampleRate);
        var vendor = Encoding.ASCII.GetBytes("tawk-mcp tests");
        var tags = new byte[8 + 4 + vendor.Length + 4];
        "OpusTags"u8.CopyTo(tags);
        BinaryPrimitives.WriteUInt32LittleEndian(tags.AsSpan(8), (uint)vendor.Length);
        vendor.CopyTo(tags, 12);
        return [head, tags];
    }

    /// <summary>
    /// A tone, one packet for each 20 ms. <paramref name="wide"/> records it at 48 kHz, in the hybrid mode
    /// phones use for voice notes.
    /// </summary>
    public static IReadOnlyList<byte[]> Tone(int packets, bool wide = false)
    {
        var rate = wide ? 48000 : SampleRate;
        var encoder = OpusCodecFactory.CreateEncoder(rate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
        if (wide)
        {
            encoder.Bitrate = 24000;
            encoder.MaxBandwidth = OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND;
            encoder.Bandwidth = OpusBandwidth.OPUS_BANDWIDTH_SUPERWIDEBAND;
        }

        var pcm = new short[rate / 50];
        var buffer = new byte[4000];
        var made = new List<byte[]>();
        for (var p = 0; p < packets; p++)
        {
            for (var i = 0; i < pcm.Length; i++)
            {
                pcm[i] = (short)(8000 * Math.Sin(2 * Math.PI * 440 * ((p * pcm.Length) + i) / rate));
            }

            var size = encoder.Encode(pcm, pcm.Length, buffer, buffer.Length);
            made.Add(buffer[..size]);
        }

        return made;
    }

    /// <summary>The packets as an Ogg file, each on a page of its own. A packet longer than <paramref name="pageBytes"/> runs over several pages.</summary>
    public static byte[] Write(IEnumerable<byte[]> packets, int pageBytes = 255 * 255)
    {
        ArgumentNullException.ThrowIfNull(packets);
        using var file = new MemoryStream();
        uint sequence = 0;
        foreach (var packet in packets)
        {
            var at = 0;
            var ends = false;
            while (!ends)
            {
                // Whole segments of 255 go on a page until the packet's last, shorter one ends it.
                var take = Math.Min(packet.Length - at, pageBytes / 255 * 255);
                ends = take < pageBytes / 255 * 255 || at + take == packet.Length && take % 255 != 0;
                var sizes = new List<byte>();
                for (var left = take; left >= 255; left -= 255)
                {
                    sizes.Add(255);
                }

                if (at + take == packet.Length)
                {
                    sizes.Add((byte)(take % 255));
                    ends = true;
                }

                var header = new byte[27];
                "OggS"u8.CopyTo(header);
                header[5] = (byte)(at > 0 ? 1 : 0);
                BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(14), 1);
                BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(18), sequence++);
                header[26] = (byte)sizes.Count;
                file.Write(header);
                file.Write([.. sizes]);
                file.Write(packet, at, take);
                at += take;
            }
        }

        return file.ToArray();
    }
}
