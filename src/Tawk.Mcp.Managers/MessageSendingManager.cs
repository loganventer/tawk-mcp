using System.Text.Json.Nodes;
using Tawk.Mcp.Core;
using Tawk.Mcp.Engines;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Managers;

public sealed class MessageSendingManager : IMessageSendingManager
{
    private readonly ITawkControl _control;
    private readonly ITranscriptFormatter _transcript;
    private readonly IScheduleJitter _jitter;

    public MessageSendingManager(ITawkControl control, ITranscriptFormatter transcript, IScheduleJitter jitter)
    {
        _control = control;
        _transcript = transcript;
        _jitter = jitter;
    }

    public async Task<string> SendMessageAsync(
        string chat, string text, string? replyTo, Action? onApprovalWaiting, CancellationToken cancellationToken)
    {
        var args = new JsonObject { ["chat"] = chat, ["text"] = text };
        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            args["reply_to"] = replyTo;
        }

        var sent = await _control.RequestAsync<SentMessage>("send_message", args, onApprovalWaiting, cancellationToken)
            .ConfigureAwait(false);
        return sent.Edited
            ? $"Sent after you edited it in tawk (message id {sent.Id}). The final text was:\n{sent.Text}"
            : $"Approved in tawk and queued to send (message id {sent.Id}). It goes out like any message sent from tawk.";
    }

    public async Task<string> ReactAsync(string messageId, string emoji, Action? onApprovalWaiting, CancellationToken cancellationToken)
    {
        var args = new JsonObject { ["message_id"] = messageId, ["emoji"] = emoji };
        await _control.RequestAsync("react", args, onApprovalWaiting, cancellationToken).ConfigureAwait(false);
        return string.IsNullOrEmpty(emoji) ? "Reaction removed." : $"Reacted with {emoji}.";
    }

    public async Task<string> ScheduleMessageAsync(
        string chat, string dueAt, string text, Action? onApprovalWaiting, CancellationToken cancellationToken)
    {
        var nudge = _jitter.Nudge(dueAt);
        ScheduledMessage scheduled;
        try
        {
            scheduled = await ScheduleAsync(chat, nudge.When, text, onApprovalWaiting, cancellationToken).ConfigureAwait(false);
        }
        catch (TawkControlException ex) when (ex.Code == ControlErrorCode.BadRequest && nudge.When != dueAt)
        {
            // An older tawk cannot read the seconds adjustment. It refuses before asking the user, so asking again costs nothing.
            nudge = new ScheduleNudge(dueAt, TimeSpan.Zero);
            scheduled = await ScheduleAsync(chat, dueAt, text, onApprovalWaiting, cancellationToken).ConfigureAwait(false);
        }

        var due = _transcript.FormatTimestamp(scheduled.DueAt);
        return scheduled.Edited
            ? $"Scheduled for {due} (id {scheduled.Id}) after you edited it in tawk.{ScheduleNudges.Describe(nudge)} The final text is:\n{scheduled.Text}"
            : $"Scheduled for {due} (id {scheduled.Id}).{ScheduleNudges.Describe(nudge)}";
    }

    private Task<ScheduledMessage> ScheduleAsync(string chat, string when, string text, Action? onApprovalWaiting, CancellationToken cancellationToken) =>
        _control.RequestAsync<ScheduledMessage>(
            "schedule_message", new JsonObject { ["chat"] = chat, ["when"] = when, ["text"] = text }, onApprovalWaiting, cancellationToken);

    public async Task<string> DraftMessageAsync(string chat, string text, CancellationToken cancellationToken)
    {
        var args = new JsonObject { ["chat"] = chat, ["text"] = text };
        await _control.RequestAsync("draft_message", args, cancellationToken).ConfigureAwait(false);
        return "The text is now the draft in that chat in tawk. The user can edit it and send it themselves; nothing has been sent.";
    }

    public async Task<string> MarkReadAsync(string chat, Action? onApprovalWaiting, CancellationToken cancellationToken)
    {
        await _control.RequestAsync("mark_read", new JsonObject { ["chat"] = chat }, onApprovalWaiting, cancellationToken)
            .ConfigureAwait(false);
        return "Marked as read.";
    }
}
