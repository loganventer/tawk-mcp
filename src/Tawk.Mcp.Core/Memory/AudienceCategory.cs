namespace Tawk.Mcp.Core.Memory;

/// <summary>An audience a voice can be tuned for. Paths nest with slashes, such as family/spouse.</summary>
public sealed record AudienceCategory(string Path, string? Description);
