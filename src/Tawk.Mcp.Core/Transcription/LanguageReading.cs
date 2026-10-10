namespace Tawk.Mcp.Core.Transcription;

/// <summary>
/// What the engine made of the language in one stretch of a voice note: the likeliest language of those
/// it may choose from, and the likeliest one that is not English, each with how sure it is (0 to 1).
/// </summary>
public sealed record LanguageReading(string? Top, float TopProbability, string? OtherThanEnglish, float OtherProbability);
