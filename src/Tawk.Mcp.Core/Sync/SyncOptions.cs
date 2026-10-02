namespace Tawk.Mcp.Core.Sync;

/// <summary>
/// Where the memory database is kept in step with, and how often.
/// </summary>
/// <remarks>
/// Sync is configured per instance and has no default destination, ever: the repository and the token
/// are always the user's own, set on that machine. When either is missing there is no sync. Never add
/// a default repository or token here; this is open source, and memory holds profiles of real people.
/// </remarks>
public sealed record SyncOptions
{
    public const string DefaultBranch = "main";
    public const int DefaultIntervalMinutes = 120;
    public const string DefaultFile = "memory.db";
    public const string DefaultApi = "https://api.github.com/";

    public string? Token { get; init; }

    /// <summary>The GitHub repository that holds the database, as owner/name. It should be a private one.</summary>
    public string? Repository { get; init; }

    public string Branch { get; init; } = DefaultBranch;

    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(DefaultIntervalMinutes);

    /// <summary>The path of the database file inside the repository.</summary>
    public string File { get; init; } = DefaultFile;

    /// <summary>The GitHub API to talk to. Another address serves a GitHub Enterprise server.</summary>
    public Uri Api { get; init; } = new(DefaultApi);

    public bool Enabled => !string.IsNullOrWhiteSpace(Token) && !string.IsNullOrWhiteSpace(Repository);
}
