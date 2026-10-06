using Microsoft.Extensions.DependencyInjection;
using Tawk.Mcp.Core.Transcription;
using Tawk.Mcp.Managers.Transcription;

namespace Tawk.Mcp.Host;

/// <summary>
/// `tawk-mcp fetch-model NAME`: downloads a Whisper model for transcribing in process, ahead of time.
/// A transcription that needs a model that is not here fetches it too, so this only saves the first wait.
/// </summary>
public static class FetchModelCommand
{
    public static async Task<int> RunAsync(TawkMcpOptions options, TextWriter output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(output);
        var services = TawkMcpComposition.BuildModels(options);
        await using (services.ConfigureAwait(false))
        {
            try
            {
                await output.WriteLineAsync($"Fetching the {options.FetchModel} model. A large one takes a while ...").ConfigureAwait(false);
                await output.WriteLineAsync(await services.GetRequiredService<ITranscriptionModelManager>()
                    .FetchAsync(options.FetchModel ?? string.Empty, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
                return 0;
            }
            catch (TranscriptionException ex)
            {
                await output.WriteLineAsync(ex.Message).ConfigureAwait(false);
                return 1;
            }
        }
    }
}
