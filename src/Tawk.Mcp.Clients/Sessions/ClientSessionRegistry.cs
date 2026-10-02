namespace Tawk.Mcp.Clients.Sessions;

/// <summary>
/// The sessions that have talked to the server, most recent last. Nothing says when an HTTP session
/// has gone for good, so the list is capped: past the cap the one heard from longest ago is let go.
/// </summary>
public sealed class ClientSessionRegistry(Action<IClientSession>? onEvicted = null, int capacity = ClientSessionRegistry.DefaultCapacity) : IClientSessionRegistry
{
    public const int DefaultCapacity = 64;

    private readonly Lock _gate = new();
    private readonly List<IClientSession> _sessions = [];

    public IReadOnlyList<IClientSession> Sessions
    {
        get
        {
            lock (_gate)
            {
                return [.. _sessions];
            }
        }
    }

    public void Add(IClientSession session)
    {
        IClientSession? evicted = null;
        lock (_gate)
        {
            // One entry per session, and the newest: the SDK hands each request its own server object.
            _sessions.Remove(session);
            _sessions.Add(session);
            if (_sessions.Count > capacity)
            {
                evicted = _sessions[0];
                _sessions.RemoveAt(0);
            }
        }

        if (evicted is not null)
        {
            onEvicted?.Invoke(evicted);
        }
    }

    public void Remove(IClientSession session)
    {
        lock (_gate)
        {
            _sessions.Remove(session);
        }
    }
}
