using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using Tawk.Mcp.Clients.Sessions;
using Tawk.Mcp.Core;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Clients.Resources;

/// <summary>
/// Sends notifications/resources/updated to subscribed sessions when a chat changes, and for every
/// subscription after tawk-mcp reconnects to tawk, since messages may have arrived meanwhile.
/// </summary>
public sealed partial class ResourceUpdatePump(IResourceSubscriptionRegistry registry, ILogger<ResourceUpdatePump> logger) : IEventSink
{
    private int? _defaultAccount;

    public async Task OnUpdateAsync(LiveUpdate update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        switch (update.Event)
        {
            case MessageEvent message:
                await NotifyAsync(TargetsFor(message.Chat.Jid, message.Account), cancellationToken).ConfigureAwait(false);
                break;
            case ChatUpdatedEvent chat:
                await NotifyAsync(TargetsFor(chat.Chat.Jid, chat.Account), cancellationToken).ConfigureAwait(false);
                break;
            case ConnectionStateEvent { State: TawkConnectionState.Connected } connected:
                _defaultAccount = connected.Hello?.DefaultAccount;      // which account the plain uris mean
                var all = registry.All();
                await NotifyAsync(all, cancellationToken).ConfigureAwait(false);
                foreach (var session in all.Select(t => t.Session).Distinct())
                {
                    await SendAsync(session, NotificationMethods.ResourceListChangedNotification, [], cancellationToken).ConfigureAwait(false);
                }

                break;
        }
    }

    // Until tawk has said which account is the default, an event is taken to be about it.
    private IReadOnlyList<ResourceTarget> TargetsFor(string jid, AccountRef? account) =>
        registry.TargetsForChat(jid, account, account is null || _defaultAccount is null || account.Id == _defaultAccount);

    private async Task NotifyAsync(IReadOnlyList<ResourceTarget> targets, CancellationToken cancellationToken)
    {
        foreach (var target in targets)
        {
            await SendAsync(
                target.Session,
                NotificationMethods.ResourceUpdatedNotification,
                new JsonObject { ["uri"] = target.Uri },
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendAsync(IClientSession session, string method, JsonObject parameters, CancellationToken cancellationToken)
    {
        try
        {
            await session.SendNotificationAsync(method, parameters, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // The session has gone; forget its subscriptions.
            LogSessionGone(ex.Message);
            registry.RemoveSession(session);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dropping a resource subscriber: {Reason}")]
    private partial void LogSessionGone(string reason);
}
