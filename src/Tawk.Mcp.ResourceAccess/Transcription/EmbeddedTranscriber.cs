using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>Transcribes inside tawk-mcp: decodes the voice note and hands the samples to the one loaded model.</summary>
public sealed class EmbeddedTranscriber(IAudioDecoder decoder, IWhisperModelHost host, TranscriptionOptions options) : ITranscriber
{
    public string Name => "embedded";

    public async Task<Transcript> TranscribeAsync(TranscriptionPassRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var samples = await Task.Run(() => decoder.Decode(request.Path, options.MaxSeconds), cancellationToken).ConfigureAwait(false);
        return await host.TranscribeAsync(samples, request, cancellationToken).ConfigureAwait(false);
    }
}
