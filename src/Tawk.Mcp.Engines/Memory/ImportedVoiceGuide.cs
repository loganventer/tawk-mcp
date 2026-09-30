namespace Tawk.Mcp.Engines.Memory;

/// <summary>A voice guide split in two: what applies everywhere, and the sections meant for particular audiences.</summary>
public sealed record ImportedVoiceGuide(string BaseGuide, IReadOnlyList<ImportedSection> Sections, IReadOnlyList<string> UnmatchedHeadings);
