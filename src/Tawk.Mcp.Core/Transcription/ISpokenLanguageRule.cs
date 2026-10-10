namespace Tawk.Mcp.Core.Transcription;

/// <summary>Decides which language a voice note is written out in, from what the engine heard in it.</summary>
public interface ISpokenLanguageRule
{
    /// <summary>The language code to transcribe in, or null to leave it to the engine.</summary>
    string? Choose(IReadOnlyList<LanguageReading> readings);
}
