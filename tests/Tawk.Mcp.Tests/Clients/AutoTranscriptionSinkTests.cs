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
        _jobs = new InMemoryTranscriptionJobStore(options, new ManualTimeProvider());
        _sink = new AutoTranscriptionSink(
            new TranscriptionManager(new TranscriptionPolicy(options), _preferences, _jobs, new TranscriptionNoticeFormatter(new UntrustedTextFence())),
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
            Assert.That(job.Request.Languages, Is.EqualTo(new[] { "af", "en" }));
            Assert.That(job.Request.Account, Is.EqualTo("2"));
        });
    }

    [Test]
    public async Task With_the_switch_off_in_tawk_nothing_starts()
    {
        _preferences.Chosen = new TranscriptionPreferences("tiny", "auto", false);

        await _sink.OnUpdateAsync(new LiveUpdate(Voice()), CancellationToken.None);

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
