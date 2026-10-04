using Tawk.Mcp.Clients.Sessions;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.Clients.Resources;

/// <summary>Which sessions are subscribed to which resources. Kept across reconnects to tawk.</summary>
public interface IResourceSubscriptionRegistry
{
    void Subscribe(string uri, IClientSession session);

    void Unsubscribe(string uri, IClientSession session);

    void RemoveSession(IClientSession session);

    /// <summary>
    /// The subscriptions affected by a change in one chat: that chat's resource and the chat list.
    /// <paramref name="account"/> is the account the chat is in, or null from a tawk with one account. The
    /// plain uris mean the default account, so they are affected only when <paramref name="isDefault"/> is true.
    /// </summary>
    IReadOnlyList<ResourceTarget> TargetsForChat(string jid, AccountRef? account = null, bool isDefault = true);

    IReadOnlyList<ResourceTarget> All();
}
