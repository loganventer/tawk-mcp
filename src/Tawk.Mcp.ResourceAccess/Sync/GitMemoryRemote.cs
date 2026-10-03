using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Core.Sync;

namespace Tawk.Mcp.ResourceAccess.Sync;

/// <summary>
/// The memory database as a file in a git repository, reached over SSH. A version is the commit that holds
/// the file. A bare repository beside the database keeps what was fetched; commits are built there without a
/// working copy, and a push that is not a fast-forward is refused by the other side, which is how two
/// machines pushing at once are kept apart.
/// </summary>
public sealed class GitMemoryRemote(IGitRunner git, SyncOptions options, string cacheDirectory, Func<string, string?> environment) : IMemoryRemote
{
    private string? _tipWithoutFile;

    private string Address => options.Repository ?? throw new MemoryException("Memory sync has no repository set.");

    private string Branch => "refs/heads/" + options.Branch;

    public async Task<string?> HeadAsync(CancellationToken cancellationToken)
    {
        await EnsureCacheAsync(cancellationToken).ConfigureAwait(false);
        _tipWithoutFile = null;
        var listed = await RunAsync(["ls-remote", Address, Branch], "reach", cancellationToken).ConfigureAwait(false);
        var tip = listed.Output.Split(['\t', ' ', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (tip is null)
        {
            return null;                                     // an empty repository, or no such branch yet
        }

        await RunAsync(["fetch", "--quiet", "--no-tags", "--depth", "1", Address, tip], "fetch from", cancellationToken).ConfigureAwait(false);
        var has = await git.RunAsync(Command(["cat-file", "-e", $"{tip}:{options.File}"]), cancellationToken).ConfigureAwait(false);
        if (has.Succeeded)
        {
            return tip;
        }

        _tipWithoutFile = tip;                               // the branch exists but does not hold the file yet
        return null;
    }

    public async Task DownloadAsync(string version, string destination, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(version);
        var command = Command(["cat-file", "blob", $"{version}:{options.File}"]) with { OutputFile = destination };
        var result = await git.RunAsync(command, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw Failed("read the memory file from", result);
        }
    }

    public async Task<string?> PushAsync(string file, string? baseVersion, string message, CancellationToken cancellationToken)
    {
        await EnsureCacheAsync(cancellationToken).ConfigureAwait(false);
        var parent = baseVersion ?? _tipWithoutFile;
        var index = Path.Combine(cacheDirectory, "sync-index-" + Guid.NewGuid().ToString("N"));
        try
        {
            var blob = await OutputAsync(["hash-object", "-w", "--", file], null, cancellationToken).ConfigureAwait(false);
            if (parent is not null)
            {
                await OutputAsync(["read-tree", parent], index, cancellationToken).ConfigureAwait(false);
            }

            await OutputAsync(["update-index", "--add", "--cacheinfo", $"100644,{blob},{options.File}"], index, cancellationToken).ConfigureAwait(false);
            var tree = await OutputAsync(["write-tree"], index, cancellationToken).ConfigureAwait(false);
            string[] commitArguments = parent is null
                ? ["commit-tree", tree, "-m", message]
                : ["commit-tree", tree, "-p", parent, "-m", message];
            var commit = await OutputAsync(commitArguments, null, cancellationToken).ConfigureAwait(false);

            // No force: when the branch moved on, the other side refuses and this machine merges again.
            var pushed = await git.RunAsync(Command(["push", "--porcelain", Address, $"{commit}:{Branch}"]), cancellationToken).ConfigureAwait(false);
            if (pushed.Succeeded)
            {
                _tipWithoutFile = null;
                return commit;
            }

            if (WasRefused(pushed))
            {
                return null;
            }

            throw Failed("push to", pushed);
        }
        finally
        {
            File.Delete(index);
        }
    }

    // "!" marks a ref the other side would not take; the words cover older gits that say it in the summary.
    private static bool WasRefused(GitResult pushed) =>
        pushed.Output.Split('\n').Any(line => line.StartsWith('!'))
        || pushed.Error.Contains("non-fast-forward", StringComparison.Ordinal)
        || pushed.Error.Contains("fetch first", StringComparison.Ordinal);

    private async Task EnsureCacheAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(Path.Combine(cacheDirectory, "HEAD")))
        {
            return;
        }

        Directory.CreateDirectory(cacheDirectory);
        var made = await git.RunAsync(
            new GitCommand(["init", "--quiet", "--bare", cacheDirectory]) { Environment = GitSshEnvironment.For(options, environment) },
            cancellationToken).ConfigureAwait(false);
        if (!made.Succeeded)
        {
            throw Failed("prepare a place to keep", made);
        }
    }

    private GitCommand Command(IReadOnlyList<string> arguments, string? index = null)
    {
        var values = new Dictionary<string, string>(GitSshEnvironment.For(options, environment), StringComparer.Ordinal);
        if (index is not null)
        {
            values["GIT_INDEX_FILE"] = index;
        }

        return new GitCommand(["--git-dir", cacheDirectory, .. arguments]) { Environment = values };
    }

    private async Task<GitResult> RunAsync(IReadOnlyList<string> arguments, string doing, CancellationToken cancellationToken)
    {
        var result = await git.RunAsync(Command(arguments), cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? result : throw Failed(doing, result);
    }

    private async Task<string> OutputAsync(IReadOnlyList<string> arguments, string? index, CancellationToken cancellationToken)
    {
        var result = await git.RunAsync(Command(arguments, index), cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? result.Output.Trim() : throw Failed("build a commit for", result);
    }

    private static MemoryException Failed(string doing, GitResult result)
    {
        var said = result.Error.Trim();
        var hint = said.Contains("Permission denied", StringComparison.Ordinal) || said.Contains("Host key verification failed", StringComparison.Ordinal)
            ? " Check that this machine's SSH key may write to the repository, and that its host is in known_hosts."
            : string.Empty;
        return new MemoryException($"Memory sync could not {doing} the repository: {(said.Length > 0 ? said : "git exit code " + result.ExitCode)}{hint}");
    }
}
