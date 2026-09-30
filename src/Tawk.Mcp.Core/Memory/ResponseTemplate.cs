namespace Tawk.Mcp.Core.Memory;

/// <summary>A reusable reply with {{placeholders}}, optionally tied to an audience and a voice.</summary>
public sealed record ResponseTemplate(
    string Name,
    string? Description,
    string? Category,
    string? Voice,
    string? Language,
    string Body,
    DateTimeOffset Updated);
