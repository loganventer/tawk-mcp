namespace Tawk.Mcp.Host;

/// <summary>A token passed in from outside, such as a container secret in TAWKMCP_TOKEN.</summary>
public sealed class FixedTokenStore(string token) : ITokenStore
{
    public string GetOrCreate() => token;

    public string Describe() => "TAWKMCP_TOKEN";
}
