namespace Tawk.Mcp.Engines.Sync;

/// <summary>
/// The rule for where memory may sync to: a git repository reached over SSH. A bare owner/name means that
/// repository on GitHub; anything else must already be an SSH address. Web addresses are refused, because
/// they would need a password or a token and sync uses neither.
/// </summary>
public static class SyncRepositoryAddress
{
    /// <summary>The SSH address for what the user wrote, or null with <paramref name="error"/> saying why not.</summary>
    public static string? Normalise(string value, out string? error)
    {
        ArgumentNullException.ThrowIfNull(value);
        error = null;
        var text = value.Trim();
        if (IsShorthand(text))
        {
            return $"git@github.com:{text}.git";
        }

        if (IsSsh(text))
        {
            return text;
        }

        error = "must be owner/name for a GitHub repository, or an SSH address such as git@github.com:owner/name.git, not " + value;
        return null;
    }

    private static bool IsShorthand(string text)
    {
        var parts = text.Split('/');
        return parts.Length == 2 && parts.All(part => part.Length > 0 && part.All(IsNameCharacter)) && !text.Contains(':', StringComparison.Ordinal);
    }

    // user@host:path, or ssh://user@host/path. Nothing that could be read as an option to git.
    private static bool IsSsh(string text)
    {
        if (text.Length == 0 || text[0] == '-' || text.Any(char.IsWhiteSpace) || text.Any(char.IsControl))
        {
            return false;
        }

        if (text.StartsWith("ssh://", StringComparison.Ordinal))
        {
            return Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Host.Length > 0 && uri.AbsolutePath.Length > 1;
        }

        var at = text.IndexOf('@', StringComparison.Ordinal);
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        return at > 0 && colon > at + 1 && colon < text.Length - 1 && !text.Contains("://", StringComparison.Ordinal)
            && text[..at].All(IsNameCharacter) && text[(at + 1)..colon].All(IsNameCharacter);
    }

    private static bool IsNameCharacter(char c) => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.';
}
