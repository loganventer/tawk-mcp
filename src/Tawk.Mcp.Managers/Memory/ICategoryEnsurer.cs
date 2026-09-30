namespace Tawk.Mcp.Managers.Memory;

/// <summary>Normalises a category path and creates it if it is new, so nothing ever points at a missing category.</summary>
public interface ICategoryEnsurer
{
    Task<string> EnsureAsync(string category, CancellationToken cancellationToken);
}
