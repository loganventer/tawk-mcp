using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.ResourceAccess.Memory;

public interface ICategoryStore
{
    Task<IReadOnlyList<AudienceCategory>> ListAsync(CancellationToken cancellationToken);

    Task<AudienceCategory?> GetAsync(string path, CancellationToken cancellationToken);

    Task UpsertAsync(AudienceCategory category, CancellationToken cancellationToken);

    /// <summary>Removes the category, the voice variants for it, and its use on contacts and templates.</summary>
    Task<bool> DeleteAsync(string path, CancellationToken cancellationToken);
}
