using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>
/// Owns the one Whisper model tawk-mcp has loaded: it loads on first use, every job and every language
/// reuses it, a different model replaces it, and it is let go when idle. There are never two.
/// </summary>
public interface IWhisperModelHost
{
    /// <summary>
    /// Transcribes 16 kHz mono samples with the model named in the request, loading it if needed. One call
    /// runs at a time, and each step is reported to <paramref name="progress"/>. Throws
    /// <see cref="TranscriptionException"/> with a reason safe to show.
    /// </summary>
    Task<Transcript> TranscribeAsync(
        ReadOnlyMemory<float> samples, TranscriptionPassRequest request, IProgress<TranscriptionProgress> progress, CancellationToken cancellationToken);

    /// <summary>Lets the model go now, if one is loaded.</summary>
    Task UnloadAsync(CancellationToken cancellationToken);
}
