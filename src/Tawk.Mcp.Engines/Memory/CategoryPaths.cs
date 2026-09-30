using System.Text.RegularExpressions;
using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>Category paths are lowercase words joined by slashes, such as family/spouse.</summary>
public static partial class CategoryPaths
{
    public static string Normalise(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var clean = path.Trim().Trim('/').ToLowerInvariant();
        if (!Valid().IsMatch(clean))
        {
            throw new MemoryException($"\"{path}\" is not a category path. Use lowercase words, digits and dashes joined by slashes, such as family/spouse.");
        }

        return clean;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]*(/[a-z0-9][a-z0-9-]*){0,4}$")]
    private static partial Regex Valid();
}
