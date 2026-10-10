using Tawk.Mcp.Clients;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.ResourceAccess;
using Tawk.Mcp.ResourceAccess.Transcription;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.Clients;

public class TranscriptHandoffSinkTests
{
    private sealed class RecordingHandoff(IAccountScope accounts) : ITranscriptHandoff
    {
        public List<(string MessageId, string Language, string Text, string? Account, bool Replace)> Handed { get; } = [];

        public Task<bool> HandOverAsync(string messageId, string language, Transcript transcript, bool replace, CancellationToken cancellationToken)
        {
            Handed.Add((messageId, language, transcript.Text, accounts.Current, replace));
            return Task.FromResult(true);
        }
    }

    private readonly AmbientAccountScope _accounts = new();

    private static TranscriptionJob Job(string? account, params TranscriptionPass[] passes) => new(
        "t1",
        new TranscriptionRequest("3EB0", account, [.. passes.Select(p => p.Language)], TranscriptionTask.Transcribe, "large-v3", null),
        TranscriptionState.Done,
        passes,
        DateTimeOffset.UnixEpoch);

    [Test]
    public async Task Each_language_that_was_written_is_handed_over_under_the_jobs_account()
    {
        var handoff = new RecordingHandoff(_accounts);
        var sink = new TranscriptHandoffSink(handoff, _accounts);
        var job = Job(
            "2",
            new TranscriptionPass("af", new Transcript("Hallo daar", null, "large-v3", 4)),
            new TranscriptionPass("en", Failure: "timed out"),
            new TranscriptionPass("nl", new Transcript("Hallo daar", null, "large-v3", 4)));

        await sink.OnUpdateAsync(new LiveUpdate(new TranscriptEvent(job, "fake")), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handoff.Handed.Select(h => h.Language), Is.EqualTo(new[] { "af", "nl" }), "a pass that failed has nothing to hand over");
            Assert.That(handoff.Handed.Select(h => h.MessageId), Is.All.EqualTo("3EB0"));
            Assert.That(handoff.Handed.Select(h => h.Account), Is.All.EqualTo("2"));
            Assert.That(handoff.Handed.Select(h => h.Replace), Is.EqualTo(new[] { true, false }),
                "the job's first transcript takes the place of what tawk kept, and the rest are added beside it");
            Assert.That(_accounts.Current, Is.Null, "the account is let go afterwards");
        });
    }

    [Test]
    public async Task Other_events_hand_nothing_over()
    {
        var handoff = new RecordingHandoff(_accounts);
        var sink = new TranscriptHandoffSink(handoff, _accounts);

        await sink.OnUpdateAsync(new LiveUpdate(Events.Message()), CancellationToken.None);
        await sink.OnUpdateAsync(new LiveUpdate(Events.Chat()), CancellationToken.None);

        Assert.That(handoff.Handed, Is.Empty);
    }
}
