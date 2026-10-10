using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.ResourceAccess.Transcription;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.ResourceAccess.Transcription;

public class TawkTranscriptHandoffTests
{
    private readonly FakeTawkControl _control = new();

    private static readonly Transcript Words = new("Hallo daar", "af", "large-v3", 4);

    private TawkTranscriptHandoff Handoff(bool tawkKeepsThem = true)
    {
        _control.Hello = _control.Hello with { Features = tawkKeepsThem ? [TawkFeatures.Transcripts] : null };
        return new TawkTranscriptHandoff(_control);
    }

    [Test]
    public async Task A_transcript_goes_to_tawk_with_its_language_and_model()
    {
        var kept = await Handoff().HandOverAsync("3EB0", "af", Words, false, CancellationToken.None);

        var sent = _control.Last("set_transcript").Args!;
        Assert.Multiple(() =>
        {
            Assert.That(kept, Is.True);
            Assert.That((string?)sent["message_id"], Is.EqualTo("3EB0"));
            Assert.That((string?)sent["language"], Is.EqualTo("af"));
            Assert.That((string?)sent["text"], Is.EqualTo("Hallo daar"));
            Assert.That((string?)sent["model"], Is.EqualTo("large-v3"));
        });
    }

    [Test]
    public async Task A_language_left_to_the_engine_is_named_by_what_it_heard()
    {
        await Handoff().HandOverAsync("3EB0", "auto", Words, false, CancellationToken.None);

        Assert.That((string?)_control.Last("set_transcript").Args!["language"], Is.EqualTo("af"));
    }

    [Test]
    public async Task A_voice_note_written_out_again_says_so()
    {
        await Handoff().HandOverAsync("3EB0", "af", Words, true, CancellationToken.None);
        var again = _control.Last("set_transcript").Args!;
        await Handoff().HandOverAsync("3EB0", "en", Words, false, CancellationToken.None);
        var beside = _control.Last("set_transcript").Args!;

        Assert.Multiple(() =>
        {
            Assert.That((bool?)again["replace"], Is.True);
            Assert.That(beside["replace"], Is.Null);
        });
    }

    [Test]
    public async Task An_older_tawk_is_sent_nothing()
    {
        var kept = await Handoff(tawkKeepsThem: false).HandOverAsync("3EB0", "af", Words, false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(kept, Is.False);
            Assert.That(_control.Requests, Is.Empty);
        });
    }

    [Test]
    public async Task A_chat_that_is_switched_off_is_not_an_error()
    {
        _control.Fail("set_transcript", new ControlError("transcripts_off", "Voice notes in this chat are not transcribed"));

        var kept = await Handoff().HandOverAsync("3EB0", "af", Words, false, CancellationToken.None);

        Assert.That(kept, Is.False);
    }

    [Test]
    public async Task Empty_words_are_not_sent()
    {
        var kept = await Handoff().HandOverAsync("3EB0", "af", Words with { Text = "  " }, false, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(kept, Is.False);
            Assert.That(_control.Requests, Is.Empty);
        });
    }
}
