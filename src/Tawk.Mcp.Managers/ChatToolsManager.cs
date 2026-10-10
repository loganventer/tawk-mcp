using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tawk.Mcp.Core;
using Tawk.Mcp.Engines;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Managers;

public sealed class ChatToolsManager(
    ITawkControl control,
    IConfirmationGate gate,
    ITranscriptFormatter transcript,
    IChatDirectoryFormatter directory,
    IUntrustedTextFence fence) : IChatToolsManager
{
    public const string OlderTawk = " (it needs tawk 0.20.0 or later).";

    public async Task<string> ListLabelsAsync(string? chat, CancellationToken cancellationToken)
    {
        await NeedAsync(TawkFeatures.Labels, "labels", cancellationToken).ConfigureAwait(false);
        var args = string.IsNullOrWhiteSpace(chat) ? null : new JsonObject { ["chat"] = chat };
        var list = await control.RequestAsync<LabelList>("list_labels", args, cancellationToken).ConfigureAwait(false);
        if (list.Labels.Count == 0)
        {
            return list.Chat is null ? "The user has no labels yet." : "That chat carries no labels.";
        }

        // A label is the user's own word, but a chat's name is someone else's, so both go inside the fence.
        var text = list.Chat is null
            ? string.Join(", ", list.Labels)
            : $"{list.Chat.Name} <{list.Chat.Jid}>: {string.Join(", ", list.Labels)}";
        return Count(list.Labels.Count, "label") + ".\n" + fence.Wrap(list.Chat is null ? "the user's labels" : "a chat's labels", text);
    }

    public async Task<string> SetLabelAsync(string chat, string label, bool carry, WriteContext context, CancellationToken cancellationToken)
    {
        await NeedAsync(TawkFeatures.Labels, "labels", cancellationToken).ConfigureAwait(false);
        var args = new JsonObject { ["chat"] = chat, ["label"] = label, ["on"] = carry };
        var outcome = await gate.ExecuteAsync("set_label", args, context.Confirmation, context.OnApprovalWaiting, cancellationToken).ConfigureAwait(false);
        return WriteResults.Describe(outcome, r =>
        {
            var now = r.ValueKind == JsonValueKind.Object && r.TryGetProperty("labels", out var labels) && labels.ValueKind == JsonValueKind.Array
                ? labels.EnumerateArray().Select(l => l.GetString()).Where(l => !string.IsNullOrEmpty(l)).ToList()
                : [];
            return (carry ? "Labelled." : "Label taken off.")
                + (now.Count == 0 ? " The chat carries no labels now." : " The chat now carries: " + string.Join(", ", now) + ".");
        });
    }

    public async Task<string> ListRemindersAsync(CancellationToken cancellationToken)
    {
        await NeedAsync(TawkFeatures.Reminders, "reminders", cancellationToken).ConfigureAwait(false);
        var list = await control.RequestAsync<ReminderList>("list_reminders", null, cancellationToken).ConfigureAwait(false);
        if (list.Reminders.Count == 0)
        {
            return "No chat is put aside.";
        }

        var lines = new StringBuilder();
        foreach (var item in list.Reminders)
        {
            lines.Append(CultureInfo.InvariantCulture, $"{item.Chat.Name} <{item.Chat.Jid}>: {Until(item.DueAt)}\n");
        }

        return Count(list.Reminders.Count, "chat") + " put aside. Each is out of the user's chat list until its time, or until its person writes.\n"
            + fence.Wrap("the chats put aside", lines.ToString().TrimEnd());
    }

    public async Task<string> SetReminderAsync(string chat, string until, WriteContext context, CancellationToken cancellationToken)
    {
        await NeedAsync(TawkFeatures.Reminders, "reminders", cancellationToken).ConfigureAwait(false);
        var args = new JsonObject { ["chat"] = chat, ["when"] = until };
        var outcome = await gate.ExecuteAsync("set_reminder", args, context.Confirmation, context.OnApprovalWaiting, cancellationToken).ConfigureAwait(false);
        return WriteResults.Describe(outcome, r => "Put aside " + Until(WriteResults.Number(r, "due_at") ?? 0) + ". It is out of the chat list until then.");
    }

    public async Task<string> CancelReminderAsync(string chat, WriteContext context, CancellationToken cancellationToken)
    {
        await NeedAsync(TawkFeatures.Reminders, "reminders", cancellationToken).ConfigureAwait(false);
        var outcome = await gate.ExecuteAsync("cancel_reminder", new JsonObject { ["chat"] = chat }, context.Confirmation, context.OnApprovalWaiting, cancellationToken)
            .ConfigureAwait(false);
        return WriteResults.Describe(outcome, _ => "The chat is back in the list.");
    }

    public async Task<string> AwaitingRepliesAsync(int? days, CancellationToken cancellationToken)
    {
        await NeedAsync(TawkFeatures.AwaitingReplies, "list of chats awaiting a reply", cancellationToken).ConfigureAwait(false);
        var args = days is { } d ? new JsonObject { ["days"] = d } : null;
        var list = await control.RequestAsync<AwaitingReplies>("awaiting_replies", args, cancellationToken).ConfigureAwait(false);
        var lead = string.Create(
            CultureInfo.InvariantCulture,
            $"{Count(list.Chats.Count, "chat")} where the user's last message has gone unanswered for {list.Days} days or more (one-to-one chats, up to two months back).");
        return list.Chats.Count == 0 ? lead : lead + "\n" + fence.Wrap("the chats awaiting a reply", directory.FormatChats(list.Chats));
    }

    private string Until(long dueAt) => dueAt == 0 ? "until its person writes" : "until " + transcript.FormatTimestamp(dueAt) + ", or sooner if its person writes";

    private static string Count(int n, string noun) => string.Create(CultureInfo.InvariantCulture, $"{n} {noun}{(n == 1 ? string.Empty : "s")}");

    private async Task NeedAsync(string feature, string what, CancellationToken cancellationToken)
    {
        var hello = await control.ConnectAsync(cancellationToken).ConfigureAwait(false);
        if (!hello.Has(feature))
        {
            throw new TawkControlException("This tawk has no " + what + OlderTawk);
        }
    }
}
