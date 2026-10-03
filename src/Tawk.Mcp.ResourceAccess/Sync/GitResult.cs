namespace Tawk.Mcp.ResourceAccess.Sync;

/// <summary>How a run of git ended: its exit code, what it printed, and what it complained about.</summary>
public sealed record GitResult(int ExitCode, string Output, string Error)
{
    public bool Succeeded => ExitCode == 0;
}
