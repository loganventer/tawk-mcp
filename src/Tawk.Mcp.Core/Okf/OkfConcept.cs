namespace Tawk.Mcp.Core.Okf;

/// <summary>
/// One unit of knowledge in the Open Knowledge Format 0.2: the frontmatter fields the format defines, the
/// producer's own keys as a JSON object, and the Markdown body. tawk-mcp keeps concepts as rows; a bundle
/// of files is written from them on request.
/// </summary>
public sealed record OkfConcept(
    string Id,
    string Type,
    string? Title,
    string? Description,
    string? Resource,
    IReadOnlyList<string> Tags,
    string GeneratedBy,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<OkfVerification> Verified,
    OkfStatus Status,
    DateTimeOffset? StaleAfter,
    IReadOnlyList<OkfSource> Sources,
    string ExtraJson,
    string Body,
    DateTimeOffset Updated);
