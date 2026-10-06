using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.Clients;

/// <summary>The words a transcription job's state, task and languages go by in events.</summary>
public static class TranscriptNames
{
    public static string Of(TranscriptionState state) => state switch
    {
        TranscriptionState.Queued => "queued",
        TranscriptionState.Running => "running",
        TranscriptionState.Done => "done",
        TranscriptionState.Partial => "partial",
        _ => "failed",
    };

    public static string Of(TranscriptionTask task) => task == TranscriptionTask.Translate ? "translate" : "transcribe";

    /// <summary>The language as asked for; one the engine detected is written auto:xx.</summary>
    public static string Language(TranscriptionPass pass)
    {
        ArgumentNullException.ThrowIfNull(pass);
        return pass.Language == TranscriptionOptions.Auto && pass.Transcript?.DetectedLanguage is { Length: > 0 and <= 12 } heard
            && heard.All(char.IsAsciiLetter)
            ? $"auto:{heard.ToLowerInvariant()}"
            : pass.Language;
    }
}
