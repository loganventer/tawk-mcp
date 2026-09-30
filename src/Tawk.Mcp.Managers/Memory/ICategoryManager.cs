using Tawk.Mcp.Core;

namespace Tawk.Mcp.Managers.Memory;

public interface ICategoryManager
{
    Task<string> ListAsync(CancellationToken cancellationToken);

    Task<string> SetAsync(string path, string? description, CancellationToken cancellationToken);

    Task<string> DeleteAsync(string path, IUserConfirmation? confirmation, CancellationToken cancellationToken);
}
