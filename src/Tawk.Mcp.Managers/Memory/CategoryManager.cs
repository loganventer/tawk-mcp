using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Engines.Memory;
using Tawk.Mcp.ResourceAccess.Memory;

namespace Tawk.Mcp.Managers.Memory;

public sealed class CategoryManager(ICategoryStore categories, IMemoryFormatter formatter, IMemoryWriteGuard guard) : ICategoryManager
{
    public async Task<string> ListAsync(CancellationToken cancellationToken) =>
        formatter.Categories(await categories.ListAsync(cancellationToken).ConfigureAwait(false));

    public async Task<string> SetAsync(string path, string? description, CancellationToken cancellationToken)
    {
        guard.EnsureWritable();
        var clean = CategoryPaths.Normalise(path);
        await categories.UpsertAsync(new AudienceCategory(clean, string.IsNullOrWhiteSpace(description) ? null : description.Trim()), cancellationToken)
            .ConfigureAwait(false);
        return $"Category {clean} saved.";
    }

    public async Task<string> DeleteAsync(string path, IUserConfirmation? confirmation, CancellationToken cancellationToken)
    {
        guard.EnsureWritable();
        var clean = CategoryPaths.Normalise(path);
        if (await categories.GetAsync(clean, cancellationToken).ConfigureAwait(false) is null)
        {
            throw new MemoryException($"There is no category {clean}.");
        }

        await MemoryConfirmations.EnsureAsync(
            confirmation, $"delete the category {clean}, its voice variants, and its use on contacts and templates", cancellationToken).ConfigureAwait(false);
        await categories.DeleteAsync(clean, cancellationToken).ConfigureAwait(false);
        return $"Category {clean} deleted.";
    }
}
