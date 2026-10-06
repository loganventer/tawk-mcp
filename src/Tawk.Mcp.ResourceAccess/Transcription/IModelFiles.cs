namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>
/// The Whisper model files on this computer. A model is fetched when the user asks for it by name, or the
/// first time a transcription needs one that is not here.
/// </summary>
public interface IModelFiles
{
    /// <summary>The folder the models are kept in.</summary>
    string Folder { get; }

    /// <summary>The file for a model by its name, such as tiny, or null when it is not installed.</summary>
    string? Find(string model);

    /// <summary>The names of the installed models.</summary>
    IReadOnlyList<string> Installed();

    /// <summary>Downloads a model by name into the folder. Throws <see cref="Core.Transcription.TranscriptionException"/> for a name it does not know.</summary>
    Task<string> FetchAsync(string model, CancellationToken cancellationToken);
}
