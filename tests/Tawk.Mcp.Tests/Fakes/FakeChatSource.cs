using Tawk.Mcp.Core;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Tests.Fakes;

/// <summary>Resolves the chats it was told about, and refuses the rest the way tawk does.</summary>
public sealed class FakeChatSource : ITawkChatSource
{
    private readonly Dictionary<string, ChatRef> _chats = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<string>> _texts = new(StringComparer.Ordinal);

    public FakeChatSource Add(string jid, string name, params string[] ownTexts)
    {
        var chat = new ChatRef(jid, name);
        _chats[jid] = chat;
        _chats[name] = chat;
        _texts[jid] = ownTexts;
        return this;
    }

    public Task<ChatRef> ResolveAsync(string chat, CancellationToken cancellationToken) =>
        _chats.TryGetValue(chat, out var found)
            ? Task.FromResult(found)
            : throw new TawkControlException(ControlErrorCode.NotFound, $"No chat matches \"{chat}\"");

    public async Task<IReadOnlyList<string>> ReadOwnTextsAsync(string chat, int limit, CancellationToken cancellationToken) =>
        _texts[(await ResolveAsync(chat, cancellationToken)).Jid];
}
