using Tawk.Mcp.Engines.Transcription;
using Tawk.Mcp.ResourceAccess.Transcription;

namespace Tawk.Mcp.Managers.Transcription;

/// <summary>Takes requests for transcriptions and answers about them. Running them is <see cref="TranscriptionRunManager"/>'s work.</summary>
public sealed class TranscriptionManager(
    ITranscriptionPolicy policy,
    ITranscriptionPreferences preferences,
    ITranscriptionJobStore jobs,
    ITranscriptionNoticeFormatter notices,
    TimeProvider clock) : ITranscriptionManager
{
    public const string NothingRunning = "No transcription is queued or running.";

    public const string NoSuchJob = "No transcription job has that id. Jobs are kept for a short while after they end, and not across a restart.";

    public async Task<string> StartAsync(
        string messageId,
        string? account,
        IReadOnlyList<string>? languages,
        string? task,
        string? model,
        string? prompt,
        CancellationToken cancellationToken)
    {
        var chosen = await preferences.ReadAsync(cancellationToken).ConfigureAwait(false);
        var request = policy.Resolve(messageId, account, languages, task, model, prompt, chosen);
        var job = jobs.Add(request, out var joined);
        return notices.Started(job, joined);
    }

    public async Task<bool> StartAutomaticAsync(string messageId, string? account, CancellationToken cancellationToken)
    {
        var chosen = await preferences.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (!policy.Automatic(chosen))
        {
            return false;
        }

        jobs.Add(policy.Resolve(messageId, account, null, null, null, null, chosen), out _);
        return true;
    }

    public string Read(string jobId) =>
        string.IsNullOrWhiteSpace(jobId) || jobs.Find(jobId) is not { } job ? NoSuchJob : notices.Describe(job);

    public string Progress(string? jobId)
    {
        var now = clock.GetUtcNow();
        if (string.IsNullOrWhiteSpace(jobId))
        {
            var active = jobs.Active();
            return active.Count == 0
                ? NothingRunning
                : string.Join('\n', active.Select(job => notices.Progress(job, jobs.Progress(job.Id), now)));
        }

        return jobs.Find(jobId) is { } found ? notices.Progress(found, jobs.Progress(found.Id), now) : NoSuchJob;
    }
}
