using System.Globalization;
using System.Text.Json.Nodes;
using Tawk.Mcp.Clients.Sessions;
using Tawk.Mcp.Clients.Workflow;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Clients.Channels;

/// <summary>
/// Pushes new WhatsApp messages into connected Claude Code sessions as channel events: what other people
/// send, and when asked for, what the user sends and who read the user's messages. A transcription an agent
/// asked for reports back the same way when it ends.
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
        var account = update.Event.Account;
        if (account is not null)
        {
            meta["account"] = account.Label;
            meta["account_id"] = account.Id.ToString(CultureInfo.InvariantCulture);
        }

        // The same contact on two accounts is two chats, each with its own history to point at.
        var seenAs = account is null ? chat : string.Create(CultureInfo.InvariantCulture, $"{account.Id}/{chat}");

        // A message or a transcript handed to the agent is a round, the same as a tool call; a read receipt is not.
        var round = update.Event is MessageEvent or TranscriptEvent;

        // Someone coming online says nothing about the chat's history, so it neither points at it nor uses up the pointer.
        var pointsAtHistory = update.Event is not (PresenceEvent or SummaryWantedEvent);
        // A summary is written once: when several sessions share this tawk-mcp, the first that takes channel events writes it.
        var listeners = sessions.Sessions.Where(Wants);
        foreach (var session in update.Event is SummaryWantedEvent ? listeners.Take(1) : listeners)
        {
            // A session's first event from a chat points at its history, in case the agent lacks it. The
            // pointer goes after the fenced text, so nothing in a message can pose as it.
            var content = pointsAtHistory && !string.IsNullOrEmpty(chat) && hints.FirstEventOf(session, seenAs!)
                ? update.ModelText + "\n" + TawkServerInstructions.ChannelContext(chat, account?.Id)
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
        PresenceEvent presence when options.Presence => Presence(presence),
        TranscriptEvent transcript => Transcript(transcript),
        SummaryWantedEvent wanted => new JsonObject
        {
            ["chat_jid"] = wanted.Chat.Jid,
            ["chat_name"] = wanted.Chat.Name,
            ["message_id"] = wanted.Message.Id,
            ["sender"] = wanted.Message.SenderName ?? wanted.Message.Sender ?? string.Empty,
            ["ts"] = wanted.Message.Ts.ToString(CultureInfo.InvariantCulture),
            ["type"] = "summary_wanted",
            ["max_chars"] = wanted.MaxChars.ToString(CultureInfo.InvariantCulture),
            ["from_me"] = "false",
        },
        _ => null,
    };

    // There is no message here, so no message id; the state says which way it went.
    private static JsonObject Presence(PresenceEvent presence)
    {
        var meta = new JsonObject
        {
            ["chat_jid"] = presence.Chat.Jid,
            ["chat_name"] = presence.Chat.Name,
            ["sender"] = presence.Who.Name ?? presence.Who.Jid,
            ["ts"] = presence.At.ToString(CultureInfo.InvariantCulture),
            ["type"] = "presence",
            ["state"] = presence.Online ? "online" : "offline",
            ["from_me"] = "false",
        };
        if (presence.LastSeen is { } seen)
        {
            meta["last_seen"] = seen.ToString(CultureInfo.InvariantCulture);
        }

        return meta;
    }

    // A job the agent asked for always reports back, so no option turns this off but the channel itself.
    private static JsonObject Transcript(TranscriptEvent transcript)
    {
        var job = transcript.Job;
        var meta = new JsonObject
        {
            ["chat_jid"] = job.Chat?.Jid ?? string.Empty,
            ["chat_name"] = job.Chat?.Name ?? string.Empty,
            ["message_id"] = job.Request.MessageId,
            ["type"] = "transcript",
            ["status"] = TranscriptNames.Of(job.State),
            ["job_id"] = job.Id,
            ["languages"] = string.Join(',', job.Passes.Select(TranscriptNames.Language)),
            ["failed_languages"] = string.Join(',', job.Passes.Where(p => p.Failure is not null).Select(p => p.Language)),
            ["engine"] = transcript.Engine,
            ["model"] = job.Request.Model,
            ["task"] = TranscriptNames.Of(job.Request.Task),
        };
        if (job.Passes.Select(p => p.Transcript?.DurationS).FirstOrDefault(d => d is not null) is { } seconds)
        {
            meta["duration_s"] = Math.Round(seconds).ToString(CultureInfo.InvariantCulture);
        }

        if (job.Request.Account is { } account)
        {
            meta["account"] = account;
        }

        return meta;
    }

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
