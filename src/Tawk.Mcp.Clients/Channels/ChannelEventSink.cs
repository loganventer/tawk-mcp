using System.Globalization;
using System.Text.Json.Nodes;
using Tawk.Mcp.Clients.Sessions;
using Tawk.Mcp.Clients.Workflow;
using Tawk.Mcp.Core;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Clients.Channels;

/// <summary>Pushes new WhatsApp messages into connected Claude Code sessions as channel events.</summary>
public sealed class ChannelEventSink(ChannelOptions options, IClientSessionRegistry sessions, IWorkflowCadence cadence) : IEventSink
{
    public async Task OnUpdateAsync(LiveUpdate update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (options.Mode == ChannelMode.Off
            || update.Event is not MessageEvent { Message.FromMe: false } message
            || update.ModelText is null)
        {
            return;
        }

        foreach (var session in sessions.Sessions.Where(Wants))
        {
            var parameters = new JsonObject
            {
                ["content"] = update.ModelText,
                ["meta"] = new JsonObject
                {
                    ["chat_jid"] = message.Chat.Jid,
                    ["chat_name"] = message.Chat.Name,
                    ["message_id"] = message.Message.Id,
                    ["sender"] = message.Message.SenderName ?? message.Message.Sender ?? string.Empty,
                    ["ts"] = message.Message.Ts.ToString(CultureInfo.InvariantCulture),
                    ["type"] = message.Message.Type,
                },
            };
            try
            {
                await session.SendNotificationAsync(ChannelOptions.Method, parameters, cancellationToken).ConfigureAwait(false);

                // A message handed to the agent is a round, the same as a tool call.
                cadence.Round();
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                sessions.Remove(session);
            }
        }
    }

    private bool Wants(IClientSession session) =>
        options.Mode == ChannelMode.On
        || (session.ClientName?.Contains("claude", StringComparison.OrdinalIgnoreCase) ?? false);
}
