using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Channels;
using Tawk.Mcp.Core;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Clients.Streaming;

/// <summary>Keeps the last events for replay and hands new ones to every /events subscriber.</summary>
public sealed class EventStreamHub : IEventSink, IEventStreamHub
{
    public const int DefaultCapacity = 200;
    private const int SubscriberBacklog = 1000;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly Lock _gate = new();
    private readonly LinkedList<StreamEvent> _buffer = new();
    private readonly List<Channel<StreamEvent>> _subscribers = [];
    private readonly int _capacity;
    private long _nextId;

    public EventStreamHub()
        : this(DefaultCapacity)
    {
    }

    public EventStreamHub(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    public Task OnUpdateAsync(LiveUpdate update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        var (name, data) = update.Event switch
        {
            MessageEvent m => ("message", JsonSerializer.Serialize(new { chat = m.Chat, message = m.Message, account = m.Account }, Json)),
            ChatUpdatedEvent c => ("chat", JsonSerializer.Serialize(new { chat = c.Chat, account = c.Account }, Json)),
            ConnectionStateEvent s => ("tawk", JsonSerializer.Serialize(new { state = TawkConnectionStates.ToWire(s.State) }, Json)),
            ReadEvent r => ("read", JsonSerializer.Serialize(new { chat = r.Chat, message_id = r.MessageId, reader = r.Reader, at = r.At, account = r.Account }, Json)),
            MessageActivityEvent a => (
                ActivityNames.Of(a.Kind),
                JsonSerializer.Serialize(new { chat = a.Chat, message_id = a.MessageId, who = a.Who, emoji = a.Emoji, message = a.Message, at = a.At, account = a.Account }, Json)),
            _ => (null, null),
        };
        if (name is not null)
        {
            Publish(name, data!);
        }

        return Task.CompletedTask;
    }

    public EventStreamSubscription Subscribe(long? lastEventId)
    {
        // A reader that has stalled loses its oldest events instead of holding every one; it can replay by event id.
        var channel = Channel.CreateBounded<StreamEvent>(
            new BoundedChannelOptions(SubscriberBacklog) { SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest });
        IReadOnlyList<StreamEvent> replay;
        lock (_gate)
        {
            replay = lastEventId is { } last ? [.. _buffer.Where(e => e.Id > last)] : [];
            _subscribers.Add(channel);
        }

        return new EventStreamSubscription(replay, channel.Reader, () =>
        {
            lock (_gate)
            {
                _subscribers.Remove(channel);
            }

            channel.Writer.TryComplete();
        });
    }

    private void Publish(string name, string data)
    {
        lock (_gate)
        {
            var item = new StreamEvent(++_nextId, name, data);
            _buffer.AddLast(item);
            while (_buffer.Count > _capacity)
            {
                _buffer.RemoveFirst();
            }

            foreach (var subscriber in _subscribers)
            {
                subscriber.Writer.TryWrite(item);
            }
        }
    }
}
