using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.Engines.Transcription;

/// <summary>
/// Decides whether a call's choices are allowed, and fills in the user's defaults. tawk's settings panel
/// comes first; tawk-mcp's own settings stand in only for what tawk does not say, as with an older tawk
/// or one that is not running; the built-in value is last.
/// </summary>
public interface ITranscriptionPolicy
{
    /// <summary>Returns the request to run, or throws <see cref="TranscriptionException"/> saying what is not allowed.</summary>
    TranscriptionRequest Resolve(
        string messageId,
        string? account,
        IReadOnlyList<string>? languages,
        string? task,
        string? model,
        string? prompt,
        TranscriptionPreferences preferences);

    /// <summary>Whether every voice note is transcribed without being asked.</summary>
    bool Automatic(TranscriptionPreferences preferences);
}
