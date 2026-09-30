using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Tawk.Mcp.Core;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Tests.Fakes;

/// <summary>Answers operations from a table, records every request, and can raise events.</summary>
public sealed class FakeTawkControl : ITawkControl
{
    private readonly Dictionary<string, Func<JsonObject?, JsonElement>> _answers = new(StringComparer.Ordinal);
    private readonly List<Channel<TawkEvent>> _readers = [];
    private readonly Lock _gate = new();

    public List<RecordedRequest> Requests { get; } = [];

    public HashSet<string> AskForApproval { get; } = new(StringComparer.Ordinal);

    public TawkConnectionState State { get; set; } = TawkConnectionState.Connected;

    public IAsyncEnumerable<TawkEvent> Events => ReadAsync();

    public int ReaderCount
    {
        get
        {
            lock (_gate)
            {
                return _readers.Count;
            }
        }
    }

    public FakeTawkControl Answer(string op, string json)
    {
        _answers[op] = _ => JsonDocument.Parse(json).RootElement.Clone();
        return this;
    }

    public FakeTawkControl Answer(string op, Func<JsonObject?, JsonElement> answer)
    {
        _answers[op] = answer;
        return this;
    }

    public FakeTawkControl Fail(string op, ControlError error)
    {
        _answers[op] = _ => throw new TawkControlException(error);
        return this;
    }

    public FakeTawkControl Fail(string op, TawkControlException exception)
    {
        _answers[op] = _ => throw exception;
        return this;
    }

    public RecordedRequest Last(string op) => Requests.Last(r => r.Op == op);

    public Task<HelloInfo> ConnectAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new HelloInfo(1, "0.6.4", "manage", new AccountInfo("27830000000@s.whatsapp.net", "Logan"), true));

    public Task<JsonElement> RequestAsync(string op, JsonObject? args, Action? onApprovalWaiting, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            Requests.Add(new RecordedRequest(op, (JsonObject?)args?.DeepClone()));
        }

        if (AskForApproval.Contains(op))
        {
            onApprovalWaiting?.Invoke();
        }

        if (!_answers.TryGetValue(op, out var answer))
        {
            return Task.FromResult(JsonDocument.Parse("{}").RootElement.Clone());
        }

        try
        {
            return Task.FromResult(answer(args));
        }
        catch (TawkControlException ex)
        {
            return Task.FromException<JsonElement>(ex);
        }
    }

    public void Raise(TawkEvent tawkEvent)
    {
        lock (_gate)
        {
            foreach (var reader in _readers)
            {
                reader.Writer.TryWrite(tawkEvent);
            }
        }
    }

    public void Complete()
    {
        lock (_gate)
        {
            foreach (var reader in _readers)
            {
                reader.Writer.TryComplete();
            }
        }
    }

    private async IAsyncEnumerable<TawkEvent> ReadAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<TawkEvent>();
        lock (_gate)
        {
            _readers.Add(channel);
        }

        await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return item;
        }
    }
}
