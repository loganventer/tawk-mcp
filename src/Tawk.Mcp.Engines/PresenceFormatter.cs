using System.Globalization;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.Engines;

public sealed class PresenceFormatter(TimeProvider? timeProvider = null) : IPresenceFormatter
{
    private readonly TimeZoneInfo _timeZone = (timeProvider ?? TimeProvider.System).LocalTimeZone;

    public string Format(ChatPresence presence)
    {
        ArgumentNullException.ThrowIfNull(presence);
        var who = PlainName.Of(string.IsNullOrWhiteSpace(presence.Chat.Name) ? presence.Chat.Jid : presence.Chat.Name);
        return presence.State switch
        {
            ChatPresence.Online => $"{who} is online on WhatsApp now.",
            ChatPresence.Offline when presence.LastSeen is { } seconds => string.Create(
                CultureInfo.InvariantCulture,
                $"{who} is offline. Last seen {TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(seconds), _timeZone):yyyy-MM-dd HH:mm}."),
            ChatPresence.Offline => $"{who} is offline, and does not share when they were last seen.",
            _ when !presence.Watching =>
                $"Whether {who} is online is not known: tawk is not shown as online itself right now (it is idle, or \"Appear online\" is off), "
                + "and WhatsApp only tells it about others while it is. Try again when the user is active in tawk.",
            _ => $"Whether {who} is online is not known: WhatsApp gave no answer, which usually means they do not share their online status with the user.",
        };
    }
}
