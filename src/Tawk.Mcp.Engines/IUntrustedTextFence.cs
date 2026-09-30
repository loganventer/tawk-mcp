namespace Tawk.Mcp.Engines;

/// <summary>Wraps text written by other people in a clearly marked block that says it is data.</summary>
public interface IUntrustedTextFence
{
    string Wrap(string label, string content);
}
