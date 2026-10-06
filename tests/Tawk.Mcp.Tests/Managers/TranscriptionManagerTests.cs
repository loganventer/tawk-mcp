using Microsoft.Extensions.Logging.Abstractions;
using Tawk.Mcp.Clients.Channels;
using Tawk.Mcp.Clients.Sessions;
using Tawk.Mcp.Clients.Workflow;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Media;
using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.Engines;
using Tawk.Mcp.Engines.Transcription;
using Tawk.Mcp.Managers;
using Tawk.Mcp.Managers.Transcription;
using Tawk.Mcp.ResourceAccess;
using Tawk.Mcp.ResourceAccess.Transcription;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Managers;

public class TranscriptionManagerTests
{
    /// <summary>The two managers over one job store, as the composition root binds them.</summary>
    private sealed record Pair(TranscriptionManager Requests, TranscriptionRunManager Runs)
    {
        public string Start(string messageId, string? account, IReadOnlyList<string>? languages, string? task, string? model, string? prompt) =>
            Requests.StartAsync(messageId, account, languages, task, model, prompt, CancellationToken.None).GetAwaiter().GetResult();

        public Task<bool> StartAutomaticAsync(string messageId) => Requests.StartAutomaticAsync(messageId, null, CancellationToken.None);

        public string Read(string jobId) => Requests.Read(jobId);

        public string Progress(string? jobId) => Requests.Progress(jobId);

        public Task RunNextAsync(CancellationToken cancellationToken) => Runs.RunNextAsync(cancellationToken);
    }

    private readonly ManualTimeProvider _clock = new();
    private readonly FakeMediaSource _media = new();
    private readonly FakeMediaFiles _files = new();
    private readonly FakeTranscriber _transcriber = new();
    private readonly RecordingSink _sink = new();
    private readonly FakeTranscriptionPreferences _preferences = new();
    private readonly AmbientAccountScope _accounts = new();
    private TranscriptionOptions _options = new() { Engine = TranscriptionEngine.Http, Timeout = TimeSpan.FromSeconds(5) };

    [SetUp]
    public void SetUp()
    {
        _media.Files["3EB0"] = new MediaFile("3EB0", "/m/voice.ogg", "audio", new ChatRef(Samples.MomJid, "Mom"));
        _files.Contents["/m/voice.ogg"] = "OggS"u8.ToArray();
    }

    private Pair Manager(params IEventSink[] sinks)
    {
        var jobs = new InMemoryTranscriptionJobStore(_options, _clock);
        var notices = new TranscriptionNoticeFormatter(new UntrustedTextFence());
        return new Pair(
            new TranscriptionManager(new TranscriptionPolicy(_options), _preferences, jobs, notices, _clock),
            new TranscriptionRunManager(
                jobs,
                _media,
                _files,
                _transcriber,
                notices,
                sinks.Length == 0 ? [_sink] : sinks,
                _accounts,
                _options,
                new MediaOptions(),
                _clock,
                NullLogger<TranscriptionRunManager>.Instance));
    }

    private TranscriptEvent Ended => (TranscriptEvent)_sink.Updates.Single().Event;

    [Test]
    public void Starting_answers_at_once_and_runs_nothing()
    {
        var answer = Manager().Start("3EB0", null, ["af", "en"], null, null, null);

        Assert.Multiple(() =>
        {
            Assert.That(answer, Does.Contain("job t1 (af, en)").And.Contain("channel event"));
            Assert.That(_transcriber.Passes, Is.Empty);
            Assert.That(_media.Asked, Is.Empty);
            Assert.That(_sink.Updates, Is.Empty);
        });
    }

    [Test]
    public async Task One_job_makes_a_transcription_for_each_language_and_announces_once()
    {
        _transcriber.Texts["af"] = "Hallo daar";
        _transcriber.Texts["en"] = "Hello there";
        var manager = Manager();
        manager.Start("3EB0", null, ["af", "en"], null, null, "Koos");

        await manager.RunNextAsync(CancellationToken.None);

        var text = _sink.Updates.Single().ModelText!;
        Assert.Multiple(() =>
        {
            Assert.That(_media.Asked, Is.EqualTo(new[] { "3EB0" }), "the file is fetched once, not once for each language");
            Assert.That(_transcriber.Passes.Select(p => p.Language), Is.EqualTo(new[] { "af", "en" }));
            Assert.That(_transcriber.Passes.Select(p => p.Prompt), Is.All.EqualTo("Koos"));
            Assert.That(Ended.Job.State, Is.EqualTo(TranscriptionState.Done));
            Assert.That(Ended.Job.Chat!.Name, Is.EqualTo("Mom"));
            Assert.That(text, Does.Contain("Language af:").And.Contain("Hallo daar").And.Contain("Language en:").And.Contain("Hello there"));
            Assert.That(manager.Read("t1"), Is.EqualTo(text));
        });
    }

    [Test]
    public async Task A_language_that_fails_leaves_the_others_and_the_job_is_partial()
    {
        _transcriber.Failures["af"] = "engine error";
        var manager = Manager();
        manager.Start("3EB0", null, ["af", "en"], null, null, null);

        await manager.RunNextAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(Ended.Job.State, Is.EqualTo(TranscriptionState.Partial));
            Assert.That(Ended.Job.Passes[0].Failure, Is.EqualTo("engine error"));
            Assert.That(Ended.Job.Passes[1].Transcript!.Text, Is.EqualTo("text in en"));
        });
    }

    [Test]
    public async Task A_pass_that_runs_too_long_is_stopped_and_the_next_still_runs()
    {
        _options = _options with { Timeout = TimeSpan.FromMilliseconds(50) };
        _transcriber.Stalls.Add("af");
        var manager = Manager();
        manager.Start("3EB0", null, ["af", "en"], null, null, null);

        await manager.RunNextAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(Ended.Job.Passes[0].Failure, Is.EqualTo("timed out"));
            Assert.That(Ended.Job.Passes[1].Transcript, Is.Not.Null);
            Assert.That(Ended.Job.State, Is.EqualTo(TranscriptionState.Partial));
        });
    }

    [Test]
    public async Task A_file_tawk_cannot_give_fails_the_job_without_a_pass()
    {
        var manager = Manager();
        manager.Start("GONE", null, null, null, null, null);
        manager.Start("PHOTO", null, null, null, null, null);
        _media.Files["PHOTO"] = new MediaFile("PHOTO", "/m/a.jpg", "image", null);

        await manager.RunNextAsync(CancellationToken.None);
        await manager.RunNextAsync(CancellationToken.None);

        var jobs = _sink.Updates.Select(u => ((TranscriptEvent)u.Event).Job).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(_transcriber.Passes, Is.Empty);
            Assert.That(jobs.Select(j => j.State), Is.All.EqualTo(TranscriptionState.Failed));
            Assert.That(jobs[0].Failure, Is.EqualTo("download failed"));
            Assert.That(jobs[1].Failure, Does.Contain("image, not a voice note"));
            Assert.That(_sink.Updates[0].ModelText, Does.Contain("failed (download failed)."));
        });
    }

    [Test]
    public async Task A_recording_longer_than_allowed_is_not_handed_over()
    {
        _options = _options with { MaxSeconds = 60 };
        _transcriber.DurationS = 61;
        var manager = Manager();
        manager.Start("3EB0", null, null, null, null, null);

        await manager.RunNextAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(Ended.Job.State, Is.EqualTo(TranscriptionState.Failed));
            Assert.That(Ended.Job.Passes.Single().Failure, Is.EqualTo("too long"));
            Assert.That(_sink.Updates.Single().ModelText, Does.Not.Contain("text in auto"));
        });
    }

    [Test]
    public async Task The_job_runs_for_the_account_it_was_asked_for()
    {
        string? during = null;
        var manager = Manager(new CallbackSink(() => during = _accounts.Current), _sink);
        manager.Start("3EB0", "work", null, null, null, null);

        await manager.RunNextAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(Ended.Job.Request.Account, Is.EqualTo("work"));
            Assert.That(_sink.Updates.Single().ModelText, Does.StartWith("On the user's account \"work\""));
            Assert.That(_accounts.Current, Is.Null, "the account does not leak out of the job");
            Assert.That(during, Is.Null, "the announcement is made after the account scope closed");
        });
    }

    [Test]
    public async Task The_model_and_languages_chosen_in_tawks_settings_are_used_and_the_switch_decides_what_is_automatic()
    {
        var manager = Manager();
        var before = await manager.StartAutomaticAsync("3EB0");
        _preferences.Chosen = new TranscriptionPreferences("small", "af,en", true);

        var after = await manager.StartAutomaticAsync("3EB0");
        await manager.RunNextAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(before, Is.False, "off in tawk, nothing is transcribed unasked");
            Assert.That(after, Is.True);
            Assert.That(_transcriber.Passes.Select(p => p.Language), Is.EqualTo(new[] { "af", "en" }));
            Assert.That(_transcriber.Passes.Select(p => p.Model), Is.All.EqualTo("small"));
        });
    }

    [Test]
    public void A_second_call_for_the_same_work_joins_the_first()
    {
        var manager = Manager();
        manager.Start("3EB0", null, ["af"], null, null, null);

        Assert.Multiple(() =>
        {
            Assert.That(manager.Start("3EB0", null, ["af"], null, null, null), Does.Contain("already under way as job t1"));
            Assert.That(manager.Start("3EB0", null, ["af", "en"], null, null, null), Does.Contain("as job t2"));
            Assert.That(manager.Read("t9"), Is.EqualTo(TranscriptionManager.NoSuchJob));
        });
    }

    [Test]
    public async Task Progress_says_the_step_a_running_job_is_on_and_carries_no_transcript()
    {
        _transcriber.Stalls.Add("af");
        _transcriber.Says = new TranscriptionProgress(TranscriptionStage.Transcribing, 40);
        var manager = Manager();
        var nothing = manager.Progress(null);
        manager.Start("3EB0", null, ["af", "en"], null, null, null);
        manager.Start("OTHER", null, ["af"], null, null, null);
        var queued = manager.Progress("t1");
        using var stop = new CancellationTokenSource();

        var running = manager.RunNextAsync(stop.Token);
        while (_transcriber.Passes.Count == 0)
        {
            await Task.Delay(5);
        }

        _clock.Advance(TimeSpan.FromSeconds(42));
        var one = manager.Progress("t1");
        var all = manager.Progress(null);
        await stop.CancelAsync();
        Assert.CatchAsync<OperationCanceledException>(async () => await running);

        Assert.Multiple(() =>
        {
            Assert.That(nothing, Is.EqualTo(TranscriptionManager.NothingRunning));
            Assert.That(queued, Is.EqualTo("Job t1 (message id 3EB0, model large-v3-turbo): queued, asked 0 s ago."));
            Assert.That(one, Is.EqualTo(
                "Job t1 (message id 3EB0, model large-v3-turbo): running, asked 42 s ago. Language af, 1 of 2: transcribing, 40% of the voice note heard."));
            Assert.That(all.Split('\n'), Is.EqualTo(new[] { one, "Job t2 (message id OTHER, model large-v3-turbo): queued, asked 42 s ago." }));
            Assert.That(manager.Progress("t9"), Is.EqualTo(TranscriptionManager.NoSuchJob));
        });
    }

    [Test]
    public async Task Progress_of_a_job_that_ended_points_to_its_transcript()
    {
        var manager = Manager();
        manager.Start("3EB0", null, ["af"], null, null, null);

        await manager.RunNextAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(manager.Progress("t1"), Is.EqualTo("Job t1 (message id 3EB0, model large-v3-turbo): done. get_transcript reads it."));
            Assert.That(manager.Progress(null), Is.EqualTo(TranscriptionManager.NothingRunning));
        });
    }

    [Test]
    public async Task The_channel_gets_one_event_with_the_languages_in_its_meta()
    {
        var sessions = new ClientSessionRegistry();
        var claude = new FakeClientSession("claude-code");
        sessions.Add(claude);
        var cadence = new WorkflowCadence(new WorkflowOptions(0, null));
        _transcriber.Failures["zu"] = "engine error";
        _transcriber.Heard = "English";
        var manager = Manager(new ChannelEventSink(new ChannelOptions(ChannelMode.Auto), sessions, cadence, new ChannelContextHints()));
        manager.Start("3EB0", "work", ["auto", "af", "zu"], "translate", null, null);

        await manager.RunNextAsync(CancellationToken.None);

        var (method, parameters) = claude.Sent.Single();
        var meta = parameters["meta"]!;
        Assert.Multiple(() =>
        {
            Assert.That(method, Is.EqualTo(ChannelOptions.Method));
            Assert.That((string?)meta["type"], Is.EqualTo("transcript"));
            Assert.That((string?)meta["status"], Is.EqualTo("partial"));
            Assert.That((string?)meta["job_id"], Is.EqualTo("t1"));
            Assert.That((string?)meta["message_id"], Is.EqualTo("3EB0"));
            Assert.That((string?)meta["chat_jid"], Is.EqualTo(Samples.MomJid));
            Assert.That((string?)meta["languages"], Is.EqualTo("auto:english,af,zu"));
            Assert.That((string?)meta["failed_languages"], Is.EqualTo("zu"));
            Assert.That((string?)meta["engine"], Is.EqualTo("fake"));
            Assert.That((string?)meta["task"], Is.EqualTo("translate"));
            Assert.That((string?)meta["account"], Is.EqualTo("work"));
            Assert.That((string?)parameters["content"], Does.Contain("<<<BEGIN UNTRUSTED").And.Contain("Language zu: failed (engine error)."));
        });
    }
}
