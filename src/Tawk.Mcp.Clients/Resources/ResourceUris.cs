namespace Tawk.Mcp.Clients.Resources;

public static class ResourceUris
{
    public const string Chats = "tawk://chats";
    public const string ChatPrefix = "tawk://chat/";
    public const string ChatTemplate = "tawk://chat/{jid}";

    public static string Chat(string jid) => ChatPrefix + jid;

    /// <summary>Returns "chats" for the chat list, the unescaped jid for one chat, or null for anything else.</summary>
    public static string? Key(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (string.Equals(uri, Chats, StringComparison.Ordinal))
        {
            return "chats";
        }

        if (uri.StartsWith(ChatPrefix, StringComparison.Ordinal) && uri.Length > ChatPrefix.Length)
        {
            return "chat:" + Uri.UnescapeDataString(uri[ChatPrefix.Length..]);
        }

        return null;
    }
}
