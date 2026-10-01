namespace Tawk.Mcp.Clients.Sessions;

public sealed class ClientSessionRegistry : IClientSessionRegistry
{
    private readonly Lock _gate = new();
    private readonly HashSet<IClientSession> _sessions = [];

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
        lock (_gate)
        {
            // One entry per session, and the newest: the SDK hands each request its own server object.
            _sessions.Remove(session);
            _sessions.Add(session);
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
