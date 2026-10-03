namespace Tawk.Mcp.ResourceAccess;

/// <summary>tawk's admin token as it is now: tawk writes a new one whenever access becomes admin.</summary>
public interface IAdminTokenSource
{
    /// <summary>The token, or null when there is none to read.</summary>
    string? Read();
}
