using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>Stands in when the user turned transcription off: every pass is refused, saying so.</summary>
public sealed class NoTranscriber : ITranscriber
{
    public const string Off = "Transcription is off: this tawk-mcp was started with --transcribe off. Only the user can turn it on.";

    public string Name => "off";

    public Task<Transcript> TranscribeAsync(TranscriptionPassRequest request, IProgress<TranscriptionProgress> progress, CancellationToken cancellationToken) =>
        Task.FromException<Transcript>(new TranscriptionException(Off));
}
