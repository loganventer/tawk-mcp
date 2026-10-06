namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>Makes Whisper's native libraries loadable before the first model is loaded.</summary>
public interface IWhisperRuntime
{
    /// <summary>
    /// Puts the libraries where they can be loaded, once. It does nothing when they already sit beside the
    /// program, as in a build that was not packed into one file.
    /// </summary>
    void Ensure();
}
