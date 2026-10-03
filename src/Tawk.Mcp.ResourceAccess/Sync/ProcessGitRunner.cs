using System.ComponentModel;
using System.Diagnostics;
using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.ResourceAccess.Sync;

/// <summary>Runs the git program found on the PATH, without a shell, and stops it when it takes too long.</summary>
public sealed class ProcessGitRunner(TimeSpan timeout) : IGitRunner
{
    public async Task<GitResult> RunAsync(GitCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in command.Arguments)
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in command.Environment)
        {
            start.Environment[name] = value;
        }

        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new MemoryException("Memory sync needs git, and it could not be started: " + ex.Message);
        }

        process.StandardInput.Close();                       // nothing ever answers a prompt
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            var error = process.StandardError.ReadToEndAsync(limit.Token);
            var output = await ReadOutputAsync(process, command.OutputFile, limit.Token).ConfigureAwait(false);
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
            return new GitResult(process.ExitCode, output, await error.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            Stop(process);
            cancellationToken.ThrowIfCancellationRequested();
            throw new MemoryException($"git did not finish within {timeout.TotalSeconds:0} s and was stopped.");
        }
    }

    private static async Task<string> ReadOutputAsync(Process process, string? outputFile, CancellationToken cancellationToken)
    {
        if (outputFile is null)
        {
            return await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        var file = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None);
        await using (file.ConfigureAwait(false))
        {
            await process.StandardOutput.BaseStream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        }

        return string.Empty;
    }

    private static void Stop(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // It had already gone.
        }
    }
}
