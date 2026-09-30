namespace Tawk.Mcp.Core.Memory;

/// <summary>Averages measured from the user's own sent messages, never the messages themselves.</summary>
public sealed record StyleBaseline(
    int Samples,
    double MeanWords,
    double StdDevWords,
    double LowercaseShare,
    double EmojiPerMessage,
    double EllipsisShare,
    double LaughShare);
