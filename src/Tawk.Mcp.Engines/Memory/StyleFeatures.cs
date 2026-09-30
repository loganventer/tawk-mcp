namespace Tawk.Mcp.Engines.Memory;

/// <summary>What was measured in one piece of text. Measuring only; judging is left to the rules.</summary>
public sealed record StyleFeatures(
    string Text,
    IReadOnlyList<string> Words,
    bool StartsUppercase,
    bool StartsLowercase,
    bool AllLowercase,
    IReadOnlyList<string> Emoji,
    int EllipsisCount,
    bool HasEmDash,
    bool HasLaugh,
    string FirstLine,
    string LastLine,
    string Language)
{
    public int WordCount => Words.Count;
}
