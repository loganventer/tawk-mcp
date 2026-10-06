using Tawk.Mcp.Engines.Media;

namespace Tawk.Mcp.Tests.Engines.Media;

public class MediaTypeSnifferTests
{
    private readonly MediaTypeSniffer _sniffer = new();

    [Test]
    public void Knows_the_pictures_a_client_can_show()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_sniffer.ImageType([0xFF, 0xD8, 0xFF, 0xE0, 0, 0]), Is.EqualTo("image/jpeg"));
            Assert.That(_sniffer.ImageType([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0]), Is.EqualTo("image/png"));
            Assert.That(_sniffer.ImageType("GIF89a...."u8), Is.EqualTo("image/gif"));
            Assert.That(_sniffer.ImageType("RIFF\0\0\0\0WEBPVP8 "u8), Is.EqualTo("image/webp"));
        });
    }

    [Test]
    public void Anything_else_is_not_a_picture()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_sniffer.ImageType("OggS\0\0\0\0"u8), Is.Null);
            Assert.That(_sniffer.ImageType("RIFF\0\0\0\0WAVEfmt "u8), Is.Null);
            Assert.That(_sniffer.ImageType([]), Is.Null);
        });
    }
}
