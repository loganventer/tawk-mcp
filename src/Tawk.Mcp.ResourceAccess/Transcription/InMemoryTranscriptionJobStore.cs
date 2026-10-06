using System.Globalization;
using System.Threading.Channels;
using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.ResourceAccess.Transcription;

public sealed class InMemoryTranscriptionJobStore(TranscriptionOptions options, TimeProvider clock) : ITranscriptionJobStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, TranscriptionJob> _jobs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _active = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TranscriptionProgress> _progress = new(StringComparer.Ordinal);
    private readonly Channel<string> _queue = Channel.CreateUnbounded<string>();
    private long _nextId;

    public TranscriptionJob Add(TranscriptionRequest request, out bool joined)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            Prune();
            if (_active.TryGetValue(request.Key, out var running) && _jobs.TryGetValue(running, out var existing))
            {
                joined = true;
                return existing;
            }

            joined = false;
            var id = string.Create(CultureInfo.InvariantCulture, $"t{++_nextId}");
            var job = new TranscriptionJob(
                id, request, TranscriptionState.Queued, [.. request.Languages.Select(l => new TranscriptionPass(l))], clock.GetUtcNow());
            _jobs[id] = job;
            _active[request.Key] = id;
            _queue.Writer.TryWrite(id);
            return job;
        }
    }

    public async Task<TranscriptionJob> TakeAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var id = await _queue.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                if (_jobs.TryGetValue(id, out var job))
                {
                    var running = job with { State = TranscriptionState.Running };
                    _jobs[id] = running;
                    return running;
                }
            }
        }
    }

    public void Save(TranscriptionJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        lock (_gate)
        {
            _jobs[job.Id] = job;
            if (job.Ended)
            {
                _active.Remove(job.Request.Key);
                _progress.Remove(job.Id);
            }
        }
    }

    public void Report(string id, TranscriptionProgress progress)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(progress);
        lock (_gate)
        {
            if (_jobs.TryGetValue(id, out var job) && !job.Ended)
            {
                _progress[id] = progress;
            }
        }
    }

    public TranscriptionProgress? Progress(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (_gate)
        {
            return _progress.GetValueOrDefault(id.Trim());
        }
    }

    public IReadOnlyList<TranscriptionJob> Active()
    {
        lock (_gate)
        {
            return [.. _jobs.Values.Where(j => !j.Ended).OrderBy(j => j.CreatedAt).ThenBy(j => j.Id.Length).ThenBy(j => j.Id, StringComparer.Ordinal)];
        }
    }

    public TranscriptionJob? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        lock (_gate)
        {
            Prune();
            return _jobs.GetValueOrDefault(id.Trim());
        }
    }

    // Ended jobs go after a while, and the oldest go first when there are too many.
    private void Prune()
    {
        var now = clock.GetUtcNow();
        var ended = _jobs.Values.Where(j => j.Ended).OrderBy(j => j.EndedAt ?? j.CreatedAt).ToList();
        var excess = ended.Count - options.KeptJobs;
        foreach (var job in ended)
        {
            if (excess > 0 || now - (job.EndedAt ?? job.CreatedAt) > options.KeptFor)
            {
                _jobs.Remove(job.Id);
                excess--;
            }
        }
    }
}
