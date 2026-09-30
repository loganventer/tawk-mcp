namespace Tawk.Mcp.Engines.Memory;

/// <summary>A filled-in template, and any placeholders nothing could fill. Missing ones are left as they were.</summary>
public sealed record TemplateRendering(string Text, IReadOnlyList<string> Missing);
