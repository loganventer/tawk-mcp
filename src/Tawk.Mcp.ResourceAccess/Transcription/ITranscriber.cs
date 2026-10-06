using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>
/// Turns one audio file into text in one language. Every implementation keeps the same promise: a
/// <see cref="Transcript"/> or a <see cref="TranscriptionException"/> with a reason safe to show, and it
/// stops when the token is cancelled.
/// </summary>
public interface ITranscriber
{
    /// <summary>A short name for the engine, shown with a transcript.</summary>
    string Name { get; }

    /// <summary>The steps of the pass are reported to <paramref name="progress"/> as it goes, as far as the engine tells.</summary>
    Task<Transcript> TranscribeAsync(TranscriptionPassRequest request, IProgress<TranscriptionProgress> progress, CancellationToken cancellationToken);
}
