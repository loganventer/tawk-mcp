namespace Tawk.Mcp.Core.Transcription;

/// <summary>How voice notes are turned into text.</summary>
public enum TranscriptionEngine
{
    Off,

    /// <summary>A transcriber the user runs, reached over HTTP with the OpenAI transcription API.</summary>
    Http,

    /// <summary>A program the user names, run once for each pass.</summary>
    Command,
}
