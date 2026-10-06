namespace Tawk.Mcp.Core.Transcription;

/// <summary>One file in one language: what a transcriber is asked for.</summary>
public sealed record TranscriptionPassRequest(string Path, string Language, TranscriptionTask Task, string Model, string? Prompt)
{
    /// <summary>The language to tell the engine, or null to let it detect one.</summary>
    public string? LanguageHint => string.Equals(Language, TranscriptionOptions.Auto, StringComparison.Ordinal) ? null : Language;
}
