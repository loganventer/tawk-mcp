using Tawk.Mcp.Core;

namespace Tawk.Mcp.ResourceAccess;

/// <summary>What the memory features need from tawk: which chat a name means, and what the user wrote in it.</summary>
public interface ITawkChatSource
{
    /// <summary>Resolves a jid or name through tawk, so hidden, locked and disallowed chats are refused as usual.</summary>
    Task<ChatRef> ResolveAsync(string chat, CancellationToken cancellationToken);

    /// <summary>The text of the user's own recent messages in a chat, newest last. Other people's messages are left out.</summary>
    Task<IReadOnlyList<string>> ReadOwnTextsAsync(string chat, int limit, CancellationToken cancellationToken);
}
