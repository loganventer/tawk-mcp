namespace Tawk.Mcp.Host;

/// <summary>
/// The user's own standing instructions for agents, in a text file they write themselves. It is the only
/// outside text that becomes instructions: nothing from chats or from memory ever does.
/// </summary>
public static class UserInstructionsFile
{
    public const int MaxLength = 8000;

    public static string DefaultPath(Func<string, string?> environment, string home)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var config = environment("XDG_CONFIG_HOME");
        var root = string.IsNullOrWhiteSpace(config) ? Path.Combine(home, ".config") : config;
        return Path.Combine(root, "tawk-mcp", "instructions.md");
    }

    /// <summary>The file's text, cut to a sensible length, or null when there is no file or it cannot be read.</summary>
    public static string? Read(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return null;
            }

            var text = File.ReadAllText(path).Trim();
            return text.Length == 0 ? null : text.Length <= MaxLength ? text : text[..MaxLength];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
