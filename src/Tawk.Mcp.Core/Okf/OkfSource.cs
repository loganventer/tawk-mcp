namespace Tawk.Mcp.Core.Okf;

/// <summary>A material a concept derives from, with the signals OKF records about it.</summary>
public sealed record OkfSource(
    string Resource,
    string? Id = null,
    string? Title = null,
    string? Author = null,
    long? UsageCount = null,
    DateTimeOffset? LastModified = null);
