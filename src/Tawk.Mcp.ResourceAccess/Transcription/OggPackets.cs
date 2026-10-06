namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>
/// Reads the packets out of an Ogg file, page by page, joining a packet that runs over the end of a page.
/// A file that stops in the middle of a page ends there.
/// </summary>
public static class OggPackets
{
    private const int HeaderSize = 27;
    private const int Lacing = 255;

    /// <summary>Every packet in the order it was written. Throws <see cref="InvalidDataException"/> where a page should start and does not.</summary>
    public static IEnumerable<byte[]> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var header = new byte[HeaderSize];
        var sizes = new byte[Lacing];
        var segment = new byte[Lacing];
        using var packet = new MemoryStream();
        while (true)
        {
            var got = stream.ReadAtLeast(header, HeaderSize, throwOnEndOfStream: false);
            if (got == 0)
            {
                yield break;
            }

            if (got < HeaderSize || !header.AsSpan(0, 4).SequenceEqual("OggS"u8))
            {
                throw new InvalidDataException("That is not an Ogg page.");
            }

            int count = header[HeaderSize - 1];
            if (stream.ReadAtLeast(sizes.AsSpan(0, count), count, throwOnEndOfStream: false) < count)
            {
                yield break;
            }

            for (var i = 0; i < count; i++)
            {
                int size = sizes[i];
                if (stream.ReadAtLeast(segment.AsSpan(0, size), size, throwOnEndOfStream: false) < size)
                {
                    yield break;
                }

                packet.Write(segment, 0, size);

                // A segment shorter than the most a segment holds ends its packet.
                if (size < Lacing)
                {
                    yield return packet.ToArray();
                    packet.SetLength(0);
                }
            }
        }
    }
}
