namespace Tawk.Mcp.Core.Memory;

/// <summary>Where a stored fact came from. A user statement always outranks an inference.</summary>
public enum FactSource
{
    Inferred,
    Imported,
    Contact,
    User,
}
