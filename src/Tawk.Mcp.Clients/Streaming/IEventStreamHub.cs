namespace Tawk.Mcp.Clients.Streaming;

public interface IEventStreamHub
{
    /// <summary>Starts a subscription. Events after <paramref name="lastEventId"/> still in the buffer are replayed first.</summary>
    EventStreamSubscription Subscribe(long? lastEventId);
}
