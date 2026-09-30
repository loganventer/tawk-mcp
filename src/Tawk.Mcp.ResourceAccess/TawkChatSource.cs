using System.Text.Json.Nodes;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.ResourceAccess;

public sealed class TawkChatSource(ITawkControl control) : ITawkChatSource
{
    public async Task<ChatRef> ResolveAsync(string chat, CancellationToken cancellationToken)
    {
        var info = await control.RequestAsync<ChatInfo>("chat_info", new JsonObject { ["chat"] = chat }, cancellationToken).ConfigureAwait(false);
        return new ChatRef(info.Chat.Jid, info.Chat.Name);
    }

    public async Task<IReadOnlyList<string>> ReadOwnTextsAsync(string chat, int limit, CancellationToken cancellationToken)
    {
        var args = new JsonObject { ["chat"] = chat, ["limit"] = Math.Clamp(limit, 1, 200) };
        var page = await control.RequestAsync<MessagePage>("read_messages", args, cancellationToken).ConfigureAwait(false);
        return page.Messages
            .Where(m => m.FromMe && !m.Deleted && !string.IsNullOrWhiteSpace(m.Text))
            .Select(m => m.Text!)
            .ToList();
    }
}
