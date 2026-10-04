using System.Globalization;
using Tawk.Mcp.Clients.Sessions;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.Clients.Resources;

public sealed class ResourceSubscriptionRegistry : IResourceSubscriptionRegistry
{
    private readonly Lock _gate = new();
    private readonly Dictionary<IClientSession, HashSet<string>> _bySession = [];

    public void Subscribe(string uri, IClientSession session)
    {
        if (ResourceUris.Parse(uri) is null)
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

    public IReadOnlyList<ResourceTarget> TargetsForChat(string jid, AccountRef? account = null, bool isDefault = true)
    {
        lock (_gate)
        {
            return [.. _bySession.SelectMany(pair => pair.Value
                .Where(uri => ResourceUris.Parse(uri) is { } address
                    && (address.Jid is null || address.Jid == jid)
                    && InAccount(address, account, isDefault))
                .Select(uri => new ResourceTarget(pair.Key, uri)))];
        }
    }

    // A plain uri means the default account; one that names an account means that account, by label or id.
    private static bool InAccount(ResourceAddress address, AccountRef? account, bool isDefault) =>
        address.Account is null
            ? account is null || isDefault
            : account is not null
              && (string.Equals(address.Account, account.Label, StringComparison.OrdinalIgnoreCase)
                  || address.Account == account.Id.ToString(CultureInfo.InvariantCulture));

    public IReadOnlyList<ResourceTarget> All()
    {
        lock (_gate)
        {
            return [.. _bySession.SelectMany(pair => pair.Value.Select(uri => new ResourceTarget(pair.Key, uri)))];
        }
    }
}
