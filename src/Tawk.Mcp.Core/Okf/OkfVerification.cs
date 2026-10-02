namespace Tawk.Mcp.Core.Okf;

/// <summary>One confirmation of a concept: who or what checked it, and when.</summary>
public sealed record OkfVerification(string By, DateTimeOffset At);
