namespace Tawk.Mcp.Core.Memory;

/// <summary>One field of a contact's profile with where it came from, how sure it is, and when it lapses.</summary>
public sealed record ContactFact(
    string Jid,
    string Field,
    string ValueJson,
    FactSource Source,
    double Confidence,
    string? Evidence,
    bool Sensitive,
    DateTimeOffset Updated,
    DateTimeOffset? Expires);
