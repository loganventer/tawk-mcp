using Tawk.Mcp.Core.Media;
using Tawk.Mcp.Engines.Media;
using Tawk.Mcp.Managers.Media;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Managers;

public class MediaViewingManagerTests
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    private readonly FakeMediaSource _media = new();
    private readonly FakeMediaFiles _files = new();

    private MediaViewingManager Manager(int maxBytes = 1000) =>
        new(_media, _files, new MediaTypeSniffer(), new MediaOptions { MaxImageBytes = maxBytes });

    [Test]
    public async Task Returns_the_picture_with_the_type_its_bytes_say()
    {
        _media.Files["3EB0"] = new MediaFile("3EB0", "/m/a.bin", "image", null);
        _files.Contents["/m/a.bin"] = Jpeg;

        var image = await Manager().ViewImageAsync(" 3EB0 ", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(image.MimeType, Is.EqualTo("image/jpeg"));
            Assert.That(image.Bytes.ToArray(), Is.EqualTo(Jpeg));
        });
    }

    [Test]
    public void A_download_under_way_says_to_call_again()
    {
        Assert.That(
            async () => await Manager().ViewImageAsync("3EB0", CancellationToken.None),
            Throws.TypeOf<MediaException>().With.Message.EqualTo(MediaViewingManager.StillDownloading));
    }

    [Test]
    public void A_voice_note_a_file_that_is_no_picture_and_a_big_one_are_refused()
    {
        _media.Files["AUDIO"] = new MediaFile("AUDIO", "/m/a.ogg", "audio", null);
        _media.Files["TEXT"] = new MediaFile("TEXT", "/m/t.jpg", "image", null);
        _media.Files["BIG"] = new MediaFile("BIG", "/m/a.bin", "image", null);
        _files.Contents["/m/t.jpg"] = "not a picture"u8.ToArray();
        _files.Contents["/m/a.bin"] = Jpeg;

        Assert.Multiple(() =>
        {
            Assert.That(async () => await Manager().ViewImageAsync("AUDIO", CancellationToken.None),
                Throws.TypeOf<MediaException>().With.Message.Contains("audio, not a picture"));
            Assert.That(async () => await Manager().ViewImageAsync("TEXT", CancellationToken.None),
                Throws.TypeOf<MediaException>().With.Message.Contains("cannot be shown"));
            Assert.That(async () => await Manager(maxBytes: 3).ViewImageAsync("BIG", CancellationToken.None), Throws.TypeOf<MediaException>());
        });
    }
}
