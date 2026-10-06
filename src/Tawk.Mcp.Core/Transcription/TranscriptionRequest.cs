namespace Tawk.Mcp.Core.Transcription;

/// <summary>
/// One call's choices after the defaults were applied and checked. <see cref="Languages"/> has one entry
/// for each transcription wanted, in the order asked for, without repeats.
/// </summary>
public sealed record TranscriptionRequest(
    string MessageId, string? Account, IReadOnlyList<string> Languages, TranscriptionTask Task, string Model, string? Prompt)
{
    /// <summary>Two requests with the same key want the same work, so the second joins the first.</summary>
    public string Key => string.Join('\u001f', MessageId, Account, string.Join(',', Languages), Task, Model, Prompt);
}
