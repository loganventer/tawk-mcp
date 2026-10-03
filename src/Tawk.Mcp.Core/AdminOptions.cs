namespace Tawk.Mcp.Core;

/// <summary>
/// Whether this instance may answer its own waiting requests in tawk. Set per instance and never by
/// default: without the path of tawk's admin token file, every write waits for the user as before.
/// </summary>
public sealed record AdminOptions(string? TokenFile)
{
    public bool Enabled => !string.IsNullOrWhiteSpace(TokenFile);
}
