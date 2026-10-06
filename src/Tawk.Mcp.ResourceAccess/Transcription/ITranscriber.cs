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

    Task<Transcript> TranscribeAsync(TranscriptionPassRequest request, CancellationToken cancellationToken);
}
