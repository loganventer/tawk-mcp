using System.Text.Json;
using Tawk.Mcp.Core;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Tests.Fakes;

public static class Events
{
    public static MessageEvent Message(string text = "See you at 6", bool fromMe = false) =>
        new(new ChatRef(Samples.MomJid, "Mom"), ControlLineCodec.Deserialize<ChatMessage>(JsonDocument.Parse(Samples.Message).RootElement) with
        {
            Text = text,
            FromMe = fromMe,
        });

    /// <summary>Mom came online, or left; with a time she was last seen when one is given.</summary>
    public static PresenceEvent Presence(bool online = true, long? lastSeen = null) =>
        new(new ChatRef(Samples.MomJid, "Mom"), new ReaderRef(Samples.MomJid, "Mom"), online, lastSeen, 1791364000);

    public static ChatUpdatedEvent Chat() =>
        new(ControlLineCodec.Deserialize<ChatSummary>(JsonDocument.Parse(Samples.Chat).RootElement));
}
