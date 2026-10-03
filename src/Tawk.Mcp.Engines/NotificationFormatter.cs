using System.Text.RegularExpressions;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.Engines;

public sealed partial class NotificationFormatter : INotificationFormatter
{
    private const int MaxNameLength = 60;

    public string Header(ChatRef chat, ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(chat);
        ArgumentNullException.ThrowIfNull(message);
        var sender = message.FromMe ? "the user (sent from their own account)" : Name(message.SenderName ?? message.Sender ?? "someone");
        return $"New WhatsApp message from {sender} in \"{Name(chat.Name)}\" (id {message.Id}):";
    }

    // Names are chosen by other people too, so they are shortened and kept to one plain line.
    private static string Name(string name)
    {
        var flat = Unsafe().Replace(name.ReplaceLineEndings(" "), " ").Trim();
        return flat.Length <= MaxNameLength ? flat : string.Concat(flat.AsSpan(0, MaxNameLength - 3), "...");
    }

    [GeneratedRegex("[<>\"`]+")]
    private static partial Regex Unsafe();
}
