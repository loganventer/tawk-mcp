using Tawk.Mcp.Core;

namespace Tawk.Mcp.Engines;

public sealed class NotificationFormatter : INotificationFormatter
{
    public string Header(ChatRef chat, ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(message);
        var sender = message.FromMe ? "the user (sent from their own account)" : Name(message.SenderName ?? message.Sender ?? "someone");
        return $"New WhatsApp message from {sender} in \"{Name(chat.Name)}\" (id {message.Id}):";
    }

    public string Read(ChatRef chat, string messageId, ReaderRef reader)
    {
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(reader);
        var who = Name(string.IsNullOrWhiteSpace(reader.Name) ? reader.Jid : reader.Name);
        return $"Read receipt: {who} read the user's message in \"{Name(chat.Name)}\" (id {Name(messageId)}).";
    }

    public string Activity(MessageActivityEvent activity)
    {
        ArgumentNullException.ThrowIfNull(activity);
        var who = Name(string.IsNullOrWhiteSpace(activity.Who?.Name) ? activity.Who?.Jid ?? "someone" : activity.Who.Name);
        var chat = Name(activity.Chat.Name);
        var id = Name(activity.MessageId);
        return activity.Kind switch
        {
            ActivityKind.Reaction when string.IsNullOrEmpty(activity.Emoji) =>
                $"Reaction removed: {who} took back their reaction to the user's message in \"{chat}\" (id {id}).",
            ActivityKind.Reaction =>
                $"Reaction: {who} reacted {Name(activity.Emoji!)} to the user's message in \"{chat}\" (id {id}).",
            ActivityKind.Edited => $"Message edited: {who} changed their message in \"{chat}\" (id {id}). It now reads:",
            ActivityKind.Deleted => $"Message deleted: {who} deleted their message in \"{chat}\" (id {id}).",
            _ => $"Scheduled message sent: the user's scheduled message {id} went out in \"{chat}\".",
        };
    }

    public string Account(AccountRef account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return $"On the user's account \"{Name(account.Label)}\" (account {account.Id}); pass that account when you act on this.";
    }

    private static string Name(string name) => PlainName.Of(name);
}
