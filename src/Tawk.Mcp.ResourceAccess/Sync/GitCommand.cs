namespace Tawk.Mcp.ResourceAccess.Sync;

/// <summary>
/// One run of git: its arguments, extra environment, and where its output goes. With
/// <see cref="OutputFile"/> the standard output is written there as bytes instead of being read as text.
/// </summary>
public sealed record GitCommand(IReadOnlyList<string> Arguments)
{
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

    public string? OutputFile { get; init; }
}
