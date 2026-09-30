namespace Tawk.Mcp.Core.Memory;

/// <summary>The part of a voice that changes for one audience category.</summary>
public sealed record VoiceVariant(
    string Voice,
    string Category,
    string Guide,
    VoiceRules Rules,
    IReadOnlyList<string> Examples,
    DateTimeOffset Updated);
