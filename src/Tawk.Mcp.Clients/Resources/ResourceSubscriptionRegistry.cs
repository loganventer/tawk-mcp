using Tawk.Mcp.Clients.Sessions;

namespace Tawk.Mcp.Clients.Resources;

public sealed class ResourceSubscriptionRegistry : IResourceSubscriptionRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<IClientSession, HashSet<string>> _bySession = [];

    public void Subscribe(string uri, IClientSession session)
    {
        if (ResourceUris.Key(uri) is null)
        {
            throw new ArgumentException($"Unknown resource: {uri}", nameof(uri));
        }

        lock (_gate)
        {
            if (!_bySession.TryGetValue(session, out var uris))
            {
                uris = new HashSet<string>(StringComparer.Ordinal);
                _bySession[session] = uris;
            }

            uris.Add(uri);
        }
    }

    public void Unsubscribe(string uri, IClientSession session)
    {
        lock (_gate)
        {
            if (_bySession.TryGetValue(session, out var uris) && uris.Remove(uri) && uris.Count == 0)
            {
                _bySession.Remove(session);
            }
        }
    }

    public void RemoveSession(IClientSession session)
    {
        lock (_gate)
        {
            _bySession.Remove(session);
        }
    }

    public IReadOnlyList<ResourceTarget> TargetsForChat(string jid)
    {
        var chatKey = "chat:" + jid;
        lock (_gate)
        {
            return [.. _bySession.SelectMany(pair => pair.Value
                .Where(uri => ResourceUris.Key(uri) is { } key && (key == "chats" || key == chatKey))
                .Select(uri => new ResourceTarget(pair.Key, uri)))];
        }
    }

    public IReadOnlyList<ResourceTarget> All()
    {
        lock (_gate)
        {
            return [.. _bySession.SelectMany(pair => pair.Value.Select(uri => new ResourceTarget(pair.Key, uri)))];
        }
    }
}
