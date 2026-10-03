using System.Collections.Concurrent;
using System.Text.Json;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.ResourceAccess;

/// <summary>Writes handed back to their caller while tawk still holds them for an answer, by request id.</summary>
internal sealed class ParkedRequests(TimeProvider clock)
{
    private const int Capacity = 64;
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public void Park(string id, string op, Task<JsonElement> answer)
    {
        if (_entries.Count >= Capacity)
        {
            Evict();
        }

        var entry = new Entry(op, clock.GetUtcNow(), answer);
        _entries[id] = entry;

        // Observed here so an answer nobody collects is never an unobserved failure.
        _ = answer.ContinueWith(
            done => entry.Outcome = done.IsCompletedSuccessfully ? "done" : Describe(done.Exception),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public Task<JsonElement>? AnswerOf(string id) => _entries.TryGetValue(id, out var entry) ? entry.Answer : null;

    public void Forget(string id) => _entries.TryRemove(id, out _);

    /// <summary>Everything parked, oldest first; those already answered are let go as they are reported.</summary>
    public IReadOnlyList<WaitingRequest> Take()
    {
        var list = new List<WaitingRequest>();
        foreach (var (id, entry) in _entries.OrderBy(pair => pair.Value.Since))
        {
            list.Add(new WaitingRequest(id, entry.Op, entry.Since, entry.Outcome));
            if (entry.Outcome is not null)
            {
                _entries.TryRemove(id, out _);
            }
        }

        return list;
    }

    private static string Describe(AggregateException? failure) =>
        failure?.InnerException is TawkControlException control
            ? ControlErrorCodes.ToWire(control.Code)
            : "failed";

    private void Evict()
    {
        var oldest = _entries
            .OrderBy(pair => pair.Value.Outcome is null)
            .ThenBy(pair => pair.Value.Since)
            .Select(pair => pair.Key)
            .FirstOrDefault();
        if (oldest is not null)
        {
            _entries.TryRemove(oldest, out _);
        }
    }

    private sealed class Entry(string op, DateTimeOffset since, Task<JsonElement> answer)
    {
        public string Op { get; } = op;

        public DateTimeOffset Since { get; } = since;

        public Task<JsonElement> Answer { get; } = answer;

        public volatile string? Outcome;
    }
}
