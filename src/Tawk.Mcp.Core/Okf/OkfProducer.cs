namespace Tawk.Mcp.Core.Okf;

/// <summary>This running tawk-mcp as an OKF actor, for what it writes on its own account.</summary>
public sealed record OkfProducer(string Version)
{
    public string Actor => OkfActors.Tool(Version);
}
