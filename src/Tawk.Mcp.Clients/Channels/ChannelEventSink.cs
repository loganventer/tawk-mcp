using System.Globalization;
using System.Text.Json.Nodes;
using Tawk.Mcp.Clients.Sessions;
using Tawk.Mcp.Clients.Workflow;
using Tawk.Mcp.Core;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Clients.Channels;

/// <summary>
/// Pushes new WhatsApp messages into connected Claude Code sessions as channel events: what other people
/// send, and when asked for, what the user sends and who read the user's messages.
/// </summary>
public sealed class ChannelEventSink(
    ChannelOptions options, IClientSessionRegistry sessions, IWorkflowCadence cadence, IChannelContextHints hints) : IEventSink
{
    public async Task OnUpdateAsync(LiveUpdate update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (options.Mode == ChannelMode.Off || update.ModelText is null || Meta(update.Event) is not { } meta)
        {
            return;
        }

        var chat = (string?)meta["chat_jid"];

        // A message handed to the agent is a round, the same as a tool call; a read receipt is not.
        var round = update.Event is MessageEvent;
        foreach (var session in sessions.Sessions.Where(Wants))
        {
            // A session's first event from a chat points at its history, in case the agent lacks it. The
            // pointer goes after the fenced text, so nothing in a message can pose as it.
            var content = !string.IsNullOrEmpty(chat) && hints.FirstEventOf(session, chat)
                ? update.ModelText + "\n" + TawkServerInstructions.ChannelContext(chat)
                : update.ModelText;
            var parameters = new JsonObject { ["content"] = content, ["meta"] = meta.DeepClone() };
            try
            {
                await session.SendNotificationAsync(ChannelOptions.Method, parameters, cancellationToken).ConfigureAwait(false);
                if (round)
                {
                    cadence.Round();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                sessions.Remove(session);
                hints.Forget(session);
            }
        }
    }

    /// <summary>The tag attributes for an event this channel carries, or null when it does not carry it.</summary>
    private JsonObject? Meta(TawkEvent tawkEvent) => tawkEvent switch
    {
        MessageEvent message when !message.Message.FromMe || options.OwnMessages => new JsonObject
        {
            ["chat_jid"] = message.Chat.Jid,
            ["chat_name"] = message.Chat.Name,
            ["message_id"] = message.Message.Id,
            ["sender"] = message.Message.SenderName ?? message.Message.Sender ?? string.Empty,
            ["ts"] = message.Message.Ts.ToString(CultureInfo.InvariantCulture),
            ["type"] = message.Message.Type,
            ["from_me"] = message.Message.FromMe ? "true" : "false",
        },
        ReadEvent read when options.ReadReceipts => new JsonObject
        {
            ["chat_jid"] = read.Chat.Jid,
            ["chat_name"] = read.Chat.Name,
            ["message_id"] = read.MessageId,
            ["sender"] = read.Reader.Name ?? read.Reader.Jid,
            ["ts"] = read.At.ToString(CultureInfo.InvariantCulture),
            ["type"] = "read",
            ["from_me"] = "false",
        },
        MessageActivityEvent activity when Carries(activity.Kind) => new JsonObject
        {
            ["chat_jid"] = activity.Chat.Jid,
            ["chat_name"] = activity.Chat.Name,
            ["message_id"] = activity.MessageId,
            ["sender"] = activity.Who?.Name ?? activity.Who?.Jid ?? string.Empty,
            ["ts"] = activity.At.ToString(CultureInfo.InvariantCulture),
            ["type"] = ActivityNames.Of(activity.Kind),
            ["from_me"] = activity.Kind == ActivityKind.ScheduledSent ? "true" : "false",
        },
        _ => null,
    };

    private bool Carries(ActivityKind kind) => kind switch
    {
        ActivityKind.Reaction => options.Reactions,
        ActivityKind.Edited or ActivityKind.Deleted => options.Edits,
        ActivityKind.ScheduledSent => options.Scheduled,
        _ => false,
    };

    private bool Wants(IClientSession session) =>
        options.Mode == ChannelMode.On
        || (session.ClientName?.Contains("claude", StringComparison.OrdinalIgnoreCase) ?? false);
}
