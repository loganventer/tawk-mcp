using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.ResourceAccess;

/// <summary>
/// Gives every reader its own copy of the event stream. The latest connection state is kept and handed
/// to each new reader first, so a reader that starts late still learns whether tawk is connected.
/// </summary>
public sealed class EventBroadcaster
{
    private readonly Lock _gate = new();
    private readonly List<Channel<TawkEvent>> _subscribers = [];
    private ConnectionStateEvent? _lastState;

    public int SubscriberCount
    {
        get
        {
            lock (_gate)
            {
                return _subscribers.Count;
            }
        }
    }

    public void Publish(TawkEvent tawkEvent)
    {
        ArgumentNullException.ThrowIfNull(tawkEvent);
        lock (_gate)
        {
            if (tawkEvent is ConnectionStateEvent state)
            {
                _lastState = state;
            }

            foreach (var subscriber in _subscribers)
            {
                subscriber.Writer.TryWrite(tawkEvent);
            }
        }
    }

    public async IAsyncEnumerable<TawkEvent> SubscribeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<TawkEvent>(new UnboundedChannelOptions { SingleReader = true });
        lock (_gate)
        {
            if (_lastState is not null)
            {
                channel.Writer.TryWrite(_lastState);
            }

            _subscribers.Add(channel);
        }

        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return item;
            }
        }
        finally
        {
            lock (_gate)
            {
                _subscribers.Remove(channel);
            }
        }
    }
}
