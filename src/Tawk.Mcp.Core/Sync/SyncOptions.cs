namespace Tawk.Mcp.Core.Sync;

/// <summary>
/// Where the memory database is kept in step with, and how often.
/// </summary>
/// <remarks>
/// Sync is configured per instance and has no default destination, ever: the repository is always the
/// user's own, set on that machine. When it is missing there is no sync. Never add a default repository
/// or key here; this is open source, and memory holds profiles of real people.
/// </remarks>
public sealed record SyncOptions
{
    public const string DefaultBranch = "main";
    public const int DefaultIntervalMinutes = 120;
    public const string DefaultFile = "memory.db";

    /// <summary>
    /// The git repository that holds the database, as an SSH address such as git@github.com:owner/name.git.
    /// It should be a private one. Access is by SSH key; there are no tokens.
    /// </summary>
    public string? Repository { get; init; }

    /// <summary>A private key to use instead of whatever SSH would pick by itself. Null leaves it to SSH.</summary>
    public string? KeyFile { get; init; }

    public string Branch { get; init; } = DefaultBranch;

    public TimeSpan Interval { get; init; } = TimeSpan.FromMinutes(DefaultIntervalMinutes);

    /// <summary>The path of the database file inside the repository.</summary>
    public string File { get; init; } = DefaultFile;

    public bool Enabled => !string.IsNullOrWhiteSpace(Repository);
}
