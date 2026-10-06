using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.ResourceAccess.Transcription;
using Tawk.Mcp.Tests.Fakes;

namespace Tawk.Mcp.Tests.ResourceAccess.Transcription;

public class InMemoryTranscriptionJobStoreTests
{
    private readonly ManualTimeProvider _clock = new();

    private static TranscriptionRequest Request(string id = "3EB0", params string[] languages) =>
        new(id, null, languages.Length == 0 ? ["auto"] : languages, TranscriptionTask.Transcribe, "tiny", null);

    private InMemoryTranscriptionJobStore Store(int kept = 50) =>
        new(new TranscriptionOptions { KeptJobs = kept, KeptFor = TimeSpan.FromMinutes(30) }, _clock);

    [Test]
    public async Task A_job_is_queued_with_a_pass_for_each_language_and_taken_in_order()
    {
        var store = Store();
        var first = store.Add(Request("A", "af", "en"), out var joined);
        store.Add(Request("B"), out _);

        var taken = await store.TakeAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(joined, Is.False);
            Assert.That(first.State, Is.EqualTo(TranscriptionState.Queued));
            Assert.That(first.Passes.Select(p => p.Language), Is.EqualTo(new[] { "af", "en" }));
            Assert.That(taken.Id, Is.EqualTo(first.Id));
            Assert.That(taken.State, Is.EqualTo(TranscriptionState.Running));
            Assert.That(store.Find(first.Id)!.State, Is.EqualTo(TranscriptionState.Running));
        });
    }

    [Test]
    public void The_same_request_joins_the_job_under_way_until_it_ends()
    {
        var store = Store();
        var first = store.Add(Request("A", "af"), out _);

        var again = store.Add(Request("A", "af"), out var joined);
        var other = store.Add(Request("A", "en"), out var otherJoined);
        store.Save(first with { State = TranscriptionState.Done, EndedAt = _clock.GetUtcNow() });
        var afterEnd = store.Add(Request("A", "af"), out var joinedAfterEnd);

        Assert.Multiple(() =>
        {
            Assert.That(joined, Is.True);
            Assert.That(again.Id, Is.EqualTo(first.Id));
            Assert.That(otherJoined, Is.False);
            Assert.That(other.Id, Is.Not.EqualTo(first.Id));
            Assert.That(joinedAfterEnd, Is.False);
            Assert.That(afterEnd.Id, Is.Not.EqualTo(first.Id));
        });
    }

    [Test]
    public void Ended_jobs_go_after_a_while_and_the_oldest_go_when_there_are_too_many()
    {
        var store = Store(kept: 2);
        var ids = new List<string>();
        foreach (var name in new[] { "A", "B", "C" })
        {
            var job = store.Add(Request(name), out _);
            store.Save(job with { State = TranscriptionState.Done, EndedAt = _clock.GetUtcNow() });
            ids.Add(job.Id);
            _clock.Advance(TimeSpan.FromMinutes(1));
        }

        Assert.Multiple(() =>
        {
            Assert.That(store.Find(ids[0]), Is.Null);
            Assert.That(store.Find(ids[1]), Is.Not.Null);
            Assert.That(store.Find(ids[2]), Is.Not.Null);
        });

        _clock.Advance(TimeSpan.FromMinutes(31));
        Assert.That(store.Find(ids[2]), Is.Null);
    }

    [Test]
    public async Task The_step_a_job_is_on_is_kept_until_the_job_ends()
    {
        var store = Store();
        store.Add(Request("A"), out _);
        store.Add(Request("B"), out _);
        var taken = await store.TakeAsync(CancellationToken.None);

        store.Report("t1", new TranscriptionProgress(TranscriptionStage.Transcribing, 30));
        store.Report("t9", new TranscriptionProgress(TranscriptionStage.LoadingModel));
        var during = store.Progress("t1");
        var active = store.Active().Select(j => j.Id).ToList();
        store.Save(taken with { State = TranscriptionState.Done, EndedAt = _clock.GetUtcNow() });
        store.Report("t1", new TranscriptionProgress(TranscriptionStage.Transcribing, 90));

        Assert.Multiple(() =>
        {
            Assert.That(during, Is.EqualTo(new TranscriptionProgress(TranscriptionStage.Transcribing, 30)));
            Assert.That(active, Is.EqualTo(new[] { "t1", "t2" }));
            Assert.That(store.Progress("t1"), Is.Null, "dropped with the job's end, and a late report is not kept");
            Assert.That(store.Progress("t9"), Is.Null, "nothing is kept for a job that does not exist");
            Assert.That(store.Active().Select(j => j.Id), Is.EqualTo(new[] { "t2" }));
        });
    }
}
