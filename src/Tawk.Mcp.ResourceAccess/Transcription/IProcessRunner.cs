namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>Runs a program without a shell. Behind an interface so a transcriber can be tested without one.</summary>
public interface IProcessRunner
{
    /// <summary>Runs the program to its end, and stops it when the token is cancelled.</summary>
    Task<ProcessResult> RunAsync(string program, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}
