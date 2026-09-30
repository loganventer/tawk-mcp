using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Engines.Memory;
using Tawk.Mcp.ResourceAccess.Memory;

namespace Tawk.Mcp.Managers.Memory;

public sealed class CategoryEnsurer(ICategoryStore categories) : ICategoryEnsurer
{
    public async Task<string> EnsureAsync(string category, CancellationToken cancellationToken)
    {
        var path = CategoryPaths.Normalise(category);
        if (await categories.GetAsync(path, cancellationToken).ConfigureAwait(false) is null)
        {
            await categories.UpsertAsync(new AudienceCategory(path, null), cancellationToken).ConfigureAwait(false);
        }

        return path;
    }
}
