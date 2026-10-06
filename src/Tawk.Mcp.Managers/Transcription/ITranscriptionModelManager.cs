namespace Tawk.Mcp.Managers.Transcription;

/// <summary>The models tawk-mcp can load to transcribe in process. Only the user fetches one, by name.</summary>
public interface ITranscriptionModelManager
{
    /// <summary>
    /// Downloads a model into the model folder and says what is installed now. Throws
    /// <see cref="Core.Transcription.TranscriptionException"/> for an unknown name or a failed download.
    /// </summary>
    Task<string> FetchAsync(string model, CancellationToken cancellationToken);
}
