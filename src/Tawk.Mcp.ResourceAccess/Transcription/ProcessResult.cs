namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>How a program ended: its exit code and what it printed.</summary>
public sealed record ProcessResult(int ExitCode, string Output);
