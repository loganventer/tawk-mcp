using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.Engines;
using Tawk.Mcp.ResourceAccess.Transcription;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.ResourceAccess.Transcription;

public class FailoverTranscriberTests
{
    private readonly ManualTimeProvider _clock = new();
    private readonly FakeTranscriber _running = new();
    private readonly FakeTranscriber _inside = new();
    private readonly FakeWhisperModelHost _host = new();
    private FailoverTranscriber _failover = null!;

    private static TranscriptionPassRequest Pass => new("/m/voice.ogg", "af", TranscriptionTask.Transcribe, "tiny", null);

    [SetUp]
    public void SetUp()
    {
        _running.Texts["af"] = "from the running transcriber";
        _inside.Texts["af"] = "from the model inside";
        _failover = new FailoverTranscriber(_running, _inside, new CircuitBreaker(2, TimeSpan.FromSeconds(60), _clock), _host);
    }

    [Test]
    public async Task A_running_transcriber_does_the_work_and_no_model_stays_loaded_inside()
    {
        var transcript = await _failover.TranscribeAsync(Pass, NoTranscriptionProgress.Instance, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(transcript.Text, Is.EqualTo("from the running transcriber"));
            Assert.That(_inside.Passes, Is.Empty);
            Assert.That(_host.Unloads, Is.EqualTo(1), "only one model is ever running");
        });
    }

    [Test]
    public async Task When_it_does_not_answer_the_same_pass_runs_on_the_model_inside()
    {
        _running.Failures["af"] = "the transcriber could not be reached";

        var transcript = await _failover.TranscribeAsync(Pass, NoTranscriptionProgress.Instance, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(transcript.Text, Is.EqualTo("from the model inside"));
            Assert.That(_failover.Name, Is.EqualTo("fake"));
            Assert.That(_host.Unloads, Is.Zero);
        });
    }

    [Test]
    public async Task After_repeated_failures_it_is_left_alone_until_the_cooldown_then_tried_once_and_taken_back()
    {
        _running.Failures["af"] = "the transcriber could not be reached";
        await _failover.TranscribeAsync(Pass, NoTranscriptionProgress.Instance, CancellationToken.None);
        await _failover.TranscribeAsync(Pass, NoTranscriptionProgress.Instance, CancellationToken.None);
        var triedWhileFailing = _running.Passes.Count;

        await _failover.TranscribeAsync(Pass, NoTranscriptionProgress.Instance, CancellationToken.None);
        var triedWhileOpen = _running.Passes.Count;
        _running.Failures.Clear();
        _clock.Advance(TimeSpan.FromSeconds(61));
        var back = await _failover.TranscribeAsync(Pass, NoTranscriptionProgress.Instance, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(triedWhileFailing, Is.EqualTo(2));
            Assert.That(triedWhileOpen, Is.EqualTo(2), "an open circuit sends work straight to the model inside");
            Assert.That(back.Text, Is.EqualTo("from the running transcriber"));
            Assert.That(_host.Unloads, Is.EqualTo(1), "its own model is let go once the running one is back");
        });
    }
}
