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

    public static ChatUpdatedEvent Chat() =>
        new(ControlLineCodec.Deserialize<ChatSummary>(JsonDocument.Parse(Samples.Chat).RootElement));
}
