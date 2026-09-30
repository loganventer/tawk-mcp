namespace Tawk.Mcp.Host;

public interface ITokenStore
{
    /// <summary>Returns the bearer token, creating and saving one on first use where the store can.</summary>
    string GetOrCreate();

    string Describe();
}
