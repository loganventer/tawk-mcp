using Microsoft.Extensions.Logging.Abstractions;
using Tawk.Mcp.Clients;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.Engines;
using Tawk.Mcp.Engines.Transcription;
using Tawk.Mcp.Managers.Transcription;
using Tawk.Mcp.ResourceAccess.Transcription;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Clients;

public class AutoTranscriptionSinkTests
{
    private readonly FakeTranscriptionPreferences _preferences = new() { Chosen = new TranscriptionPreferences("tiny", "af,en", true) };
    private readonly InMemoryTranscriptionJobStore _jobs;
    private readonly AutoTranscriptionSink _sink;

    public AutoTranscriptionSinkTests()
    {
        var options = new TranscriptionOptions { Engine = TranscriptionEngine.Http };
        var clock = new ManualTimeProvider();
        _jobs = new InMemoryTranscriptionJobStore(options, clock);
        _sink = new AutoTranscriptionSink(
            new TranscriptionManager(new TranscriptionPolicy(options), _preferences, _jobs, new TranscriptionNoticeFormatter(new UntrustedTextFence()), clock),
            NullLogger<AutoTranscriptionSink>.Instance);
    }

    private static MessageEvent Voice(bool fromMe = false, string type = "audio")
    {
        var message = Events.Message(fromMe: fromMe);
        return message with { Message = message.Message with { Type = type, Text = null } };
    }

    [Test]
    public async Task A_voice_note_from_someone_else_starts_a_job_in_the_default_languages()
    {
        await _sink.OnUpdateAsync(new LiveUpdate(Voice() with { Account = new AccountRef(2, "work") }), CancellationToken.None);

        var job = _jobs.Find("t1")!;
        Assert.Multiple(() =>
        {
            Assert.That(job.Request.MessageId, Is.EqualTo("3EB0C2A1F0"));
            Assert.That(job.Request.Languages, Is.EqualTo(new[] { "auto" }), "one transcript, in the language spoken");
            Assert.That(job.Request.Among, Is.EqualTo(new[] { "af", "en" }), "worked out among the languages listed in tawk");
            Assert.That(job.Request.Account, Is.EqualTo("2"));
        });
    }

    [Test]
    public async Task The_languages_the_user_named_for_the_chat_come_first()
    {
        await _sink.OnUpdateAsync(new LiveUpdate(Voice() with { Languages = ["zu", "en"] }), CancellationToken.None);

        Assert.That(_jobs.Find("t1")!.Request.Among, Is.EqualTo(new[] { "zu", "en" }));
    }

    [Test]
    public async Task With_the_switch_off_in_tawk_nothing_starts()
    {
        _preferences.Chosen = new TranscriptionPreferences("tiny", "auto", false);

        await _sink.OnUpdateAsync(new LiveUpdate(Voice()), CancellationToken.None);

        Assert.That(_jobs.Find("t1"), Is.Null);
    }

    [Test]
    public async Task An_older_voice_note_the_user_looked_at_starts_a_job_under_the_same_switch()
    {
        var older = new TranscriptWantedEvent(new ChatRef(Samples.MomJid, "Mom"), "OLD1") { Account = new AccountRef(2, "work") };

        await _sink.OnUpdateAsync(new LiveUpdate(older), CancellationToken.None);
        _preferences.Chosen = new TranscriptionPreferences("tiny", "auto", false);
        await _sink.OnUpdateAsync(new LiveUpdate(older with { MessageId = "OLD2" }), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(_jobs.Find("t1")!.Request.MessageId, Is.EqualTo("OLD1"));
            Assert.That(_jobs.Find("t1")!.Request.Account, Is.EqualTo("2"));
            Assert.That(_jobs.Find("t2"), Is.Null, "with automatic transcription off, looking at one starts nothing");
        });
    }

    [Test]
    public async Task A_voice_note_in_a_chat_switched_off_in_tawk_starts_nothing()
    {
        await _sink.OnUpdateAsync(new LiveUpdate(Voice() with { Transcribe = false }), CancellationToken.None);

        Assert.That(_jobs.Find("t1"), Is.Null);
    }

    [Test]
    public async Task The_users_own_voice_notes_and_other_messages_start_nothing()
    {
        await _sink.OnUpdateAsync(new LiveUpdate(Voice(fromMe: true)), CancellationToken.None);
        await _sink.OnUpdateAsync(new LiveUpdate(Voice(type: "image")), CancellationToken.None);
        await _sink.OnUpdateAsync(new LiveUpdate(Events.Message()), CancellationToken.None);
        await _sink.OnUpdateAsync(new LiveUpdate(Events.Chat()), CancellationToken.None);

        Assert.That(_jobs.Find("t1"), Is.Null);
    }
}
