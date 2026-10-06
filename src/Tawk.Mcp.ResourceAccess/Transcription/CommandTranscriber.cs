using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>
/// Runs the user's own program once for each pass and takes what it prints as the text. The command comes
/// only from the user's settings; the placeholders are filled as whole arguments, never through a shell.
/// </summary>
public sealed class CommandTranscriber(IProcessRunner runner, TranscriptionOptions options) : ITranscriber
{
    public string Name => "command";

    public async Task<Transcript> TranscribeAsync(TranscriptionPassRequest request, IProgress<TranscriptionProgress> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(progress);
        var parts = (options.Command ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            throw new TranscriptionException("no transcribe command is set");
        }

        var model = request.Model;
        var arguments = parts.Skip(1).Select(part => part
            .Replace("{file}", request.Path, StringComparison.Ordinal)
            .Replace("{language}", request.Language, StringComparison.Ordinal)
            .Replace("{model}", model, StringComparison.Ordinal)
            .Replace("{task}", request.Task == TranscriptionTask.Translate ? "translate" : "transcribe", StringComparison.Ordinal)
            .Replace("{prompt}", request.Prompt ?? string.Empty, StringComparison.Ordinal)).ToList();

        var result = await runner.RunAsync(parts[0], arguments, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new TranscriptionException($"the transcribe command ended with code {result.ExitCode}");
        }

        return new Transcript(result.Output.Trim(), null, model, null);
    }
}
