using Tawk.Mcp.Core;

namespace Tawk.Mcp.Managers;

public interface IProfileManager
{
    Task<string> GetProfileAsync(CancellationToken cancellationToken);

    Task<string> SetProfileAsync(string? name, string? about, WriteContext context, CancellationToken cancellationToken);

    Task<string> SetProfilePhotoAsync(string file, WriteContext context, CancellationToken cancellationToken);

    Task<string> RemoveProfilePhotoAsync(WriteContext context, CancellationToken cancellationToken);
}
