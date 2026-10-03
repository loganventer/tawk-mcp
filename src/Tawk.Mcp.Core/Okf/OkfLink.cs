using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Core.Okf;

/// <summary>
/// A link from one concept to another. OKF links carry no type; the label holds the words that say what
/// kind of relation it is, such as "about" or "friend of".
/// </summary>
public sealed record OkfLink(
    string FromId,
    string ToId,
    string Label,
    string? Note,
    FactSource Source,
    double Confidence,
    DateTimeOffset Updated)
{
    /// <summary>The jid of the user's account it was learnt through, or null when that is not known.</summary>
    public string? Account { get; init; }
}
