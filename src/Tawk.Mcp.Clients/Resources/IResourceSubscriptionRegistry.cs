using Tawk.Mcp.Clients.Sessions;

namespace Tawk.Mcp.Clients.Resources;

/// <summary>Which sessions are subscribed to which resources. Kept across reconnects to tawk.</summary>
public interface IResourceSubscriptionRegistry
{
    void Subscribe(string uri, IClientSession session);

    void Unsubscribe(string uri, IClientSession session);

    void RemoveSession(IClientSession session);

    /// <summary>The subscriptions affected by a change in one chat: that chat's resource and the chat list.</summary>
    IReadOnlyList<ResourceTarget> TargetsForChat(string jid);

    IReadOnlyList<ResourceTarget> All();
}
