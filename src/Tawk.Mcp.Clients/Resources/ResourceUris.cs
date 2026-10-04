namespace Tawk.Mcp.Clients.Resources;

public static class ResourceUris
{
    public const string Chats = "tawk://chats";
    public const string ChatPrefix = "tawk://chat/";
    public const string ChatTemplate = "tawk://chat/{jid}";

    // The same two for one of the user's accounts, by label or id. The forms above mean the default account.
    public const string AccountChatsTemplate = "tawk://account/{account}/chats";
    public const string AccountChatTemplate = "tawk://account/{account}/chat/{jid}";

    public static string Chat(string jid) => ChatPrefix + jid;

    private const string AccountPrefix = "tawk://account/";

    /// <summary>Reads a chat list or chat uri, in its plain form or for one account, or returns null for anything else.</summary>
    public static ResourceAddress? Parse(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (string.Equals(uri, Chats, StringComparison.Ordinal))
        {
            return new ResourceAddress(null, null);
        }

        if (uri.StartsWith(ChatPrefix, StringComparison.Ordinal) && uri.Length > ChatPrefix.Length)
        {
            return new ResourceAddress(null, Uri.UnescapeDataString(uri[ChatPrefix.Length..]));
        }

        if (!uri.StartsWith(AccountPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = uri[AccountPrefix.Length..];
        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        if (slash <= 0)
        {
            return null;
        }

        var account = Uri.UnescapeDataString(rest[..slash]);
        var tail = rest[(slash + 1)..];
        if (tail == "chats")
        {
            return new ResourceAddress(account, null);
        }

        return tail.StartsWith("chat/", StringComparison.Ordinal) && tail.Length > 5
            ? new ResourceAddress(account, Uri.UnescapeDataString(tail[5..]))
            : null;
    }
}
