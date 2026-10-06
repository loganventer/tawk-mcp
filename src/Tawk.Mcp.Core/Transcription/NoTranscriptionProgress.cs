namespace Tawk.Mcp.Core.Transcription;

/// <summary>Stands in where nobody follows a transcription's progress.</summary>
public sealed class NoTranscriptionProgress : IProgress<TranscriptionProgress>
{
    public static NoTranscriptionProgress Instance { get; } = new();

    public void Report(TranscriptionProgress value)
    {
    }
}
