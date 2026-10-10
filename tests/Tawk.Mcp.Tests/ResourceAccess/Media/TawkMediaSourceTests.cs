using System.Text.Json;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Media;
using Tawk.Mcp.ResourceAccess.Media;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.ResourceAccess.Media;

public class TawkMediaSourceTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    [Test]
    public async Task A_file_tawk_already_has_is_named_in_the_answer()
    {
        var control = new FakeTawkControl().Answer(
            "download_media", """{"path":"/cache/media/a.jpg","type":"image","chat":{"jid":"27820000000@s.whatsapp.net","name":"Mom"}}""");

        var file = await new TawkMediaSource(control).LocateAsync("3EB0", Wait, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(file, Is.EqualTo(new MediaFile("3EB0", "/cache/media/a.jpg", "image", new ChatRef(Samples.MomJid, "Mom"))));
            Assert.That((string?)control.Last("download_media").Args!["message_id"], Is.EqualTo("3EB0"));
            Assert.That(control.ReaderCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task A_chat_whose_voice_notes_are_not_transcribed_says_so_either_way()
    {
        var here = new FakeTawkControl().Answer(
            "download_media", """{"path":"/cache/media/a.ogg","type":"audio","chat":{"jid":"27820000000@s.whatsapp.net","name":"Mom"},"transcribe":false}""");
        var later = new FakeTawkControl();
        later.Answer("download_media", _ =>
        {
            later.Raise(new MediaReadyEvent(null, "3EB0", "/cache/media/a.ogg", "audio") { Transcribe = false });
            return JsonDocument.Parse("{}").RootElement.Clone();
        });

        var already = await new TawkMediaSource(here).LocateAsync("3EB0", Wait, CancellationToken.None);
        var fetched = await new TawkMediaSource(later).LocateAsync("3EB0", Wait, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(already!.Transcribe, Is.False);
            Assert.That(fetched!.Transcribe, Is.False);
        });
    }

    [Test]
    public async Task A_download_that_ends_later_is_named_by_the_media_ready_event()
    {
        var control = new FakeTawkControl();
        control.Answer("download_media", _ =>
        {
            // The event may arrive before the answer is read; it must not be missed.
            control.Raise(new MediaReadyEvent(null, "OTHER", "/cache/media/other.ogg", "audio"));
            control.Raise(new MediaReadyEvent(new ChatRef(Samples.MomJid, "Mom"), "3EB0", "/cache/media/a.ogg", "audio"));
            return JsonDocument.Parse("{}").RootElement.Clone();
        });

        var file = await new TawkMediaSource(control).LocateAsync("3EB0", Wait, CancellationToken.None);

        Assert.That(file?.Path, Is.EqualTo("/cache/media/a.ogg"));
    }

    [Test]
    public async Task A_download_still_under_way_at_the_end_of_the_wait_is_null()
    {
        var file = await new TawkMediaSource(new FakeTawkControl()).LocateAsync("3EB0", TimeSpan.FromMilliseconds(50), CancellationToken.None);

        Assert.That(file, Is.Null);
    }

    [Test]
    public void A_refusal_from_tawk_is_passed_on()
    {
        var control = new FakeTawkControl().Fail("download_media", new ControlError("not_found", "No message matches"));

        Assert.That(
            async () => await new TawkMediaSource(control).LocateAsync("3EB0", Wait, CancellationToken.None),
            Throws.TypeOf<TawkControlException>());
    }
}
