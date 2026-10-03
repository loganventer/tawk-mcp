namespace Tawk.Mcp.ResourceAccess.Sync;

/// <summary>Runs git. Behind an interface so sync can be tested without a network or a real repository host.</summary>
public interface IGitRunner
{
    /// <summary>Runs the command to its end. A failure is reported in the result, never thrown.</summary>
    Task<GitResult> RunAsync(GitCommand command, CancellationToken cancellationToken);
}
