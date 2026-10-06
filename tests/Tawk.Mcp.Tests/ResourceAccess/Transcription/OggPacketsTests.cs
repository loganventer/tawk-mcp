using Tawk.Mcp.ResourceAccess.Transcription;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.ResourceAccess.Transcription;

public class OggPacketsTests
{
    private static List<byte[]> Read(byte[] file)
    {
        using var stream = new MemoryStream(file);
        return [.. OggPackets.Read(stream)];
    }

    private static byte[] Bytes(int count, byte fill) => [.. Enumerable.Repeat(fill, count)];

    [Test]
    public void Packets_come_out_whole_and_in_order_also_when_one_runs_over_several_pages()
    {
        byte[][] packets = [Bytes(10, 1), Bytes(255, 2), Bytes(1000, 3), [], Bytes(7, 4)];

        var read = Read(OggFiles.Write(packets, pageBytes: 510));

        Assert.That(read, Is.EqualTo(packets));
    }

    [Test]
    public void A_file_cut_short_ends_with_the_last_whole_packet()
    {
        var file = OggFiles.Write([Bytes(10, 1), Bytes(40, 2)]);

        var read = Read(file[..^5]);

        Assert.That(read, Is.EqualTo(new[] { Bytes(10, 1) }));
    }

    [Test]
    public void Bytes_that_are_not_a_page_are_refused()
    {
        var file = OggFiles.Write([Bytes(10, 1)]).Concat("this is not a page of an Ogg file"u8.ToArray()).ToArray();

        Assert.That(() => Read(file), Throws.TypeOf<InvalidDataException>());
    }
}
