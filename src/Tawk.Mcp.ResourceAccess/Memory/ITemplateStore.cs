using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.ResourceAccess.Memory;

public interface ITemplateStore
{
    Task<IReadOnlyList<ResponseTemplate>> ListAsync(string? category, CancellationToken cancellationToken);

    Task<ResponseTemplate?> GetAsync(string name, CancellationToken cancellationToken);

    Task UpsertAsync(ResponseTemplate responseTemplate, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string name, CancellationToken cancellationToken);
}
