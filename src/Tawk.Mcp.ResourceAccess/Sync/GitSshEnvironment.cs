using Tawk.Mcp.Core.Sync;

namespace Tawk.Mcp.ResourceAccess.Sync;

/// <summary>
/// The environment git runs in for sync: nothing may stop to ask a question, SSH never prompts, and the
/// user's own key file is used when they named one. A GIT_SSH_COMMAND the user set themselves is left alone.
/// </summary>
public static class GitSshEnvironment
{
    public const string Author = "tawk-mcp";
    public const string AuthorEmail = "tawk-mcp@localhost";

    public static IReadOnlyDictionary<string, string> For(SyncOptions options, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["GIT_TERMINAL_PROMPT"] = "0",
            ["GIT_AUTHOR_NAME"] = Author,
            ["GIT_AUTHOR_EMAIL"] = AuthorEmail,
            ["GIT_COMMITTER_NAME"] = Author,
            ["GIT_COMMITTER_EMAIL"] = AuthorEmail,
        };
        if (!string.IsNullOrWhiteSpace(options.KeyFile))
        {
            values["GIT_SSH_COMMAND"] = $"ssh -i {Quote(options.KeyFile)} -o IdentitiesOnly=yes -o BatchMode=yes";
        }
        else if (string.IsNullOrEmpty(environment("GIT_SSH_COMMAND")))
        {
            values["GIT_SSH_COMMAND"] = "ssh -o BatchMode=yes";
        }

        return values;
    }

    // git hands this line to a shell, so the path is wrapped in single quotes.
    private static string Quote(string path) => "'" + path.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}
