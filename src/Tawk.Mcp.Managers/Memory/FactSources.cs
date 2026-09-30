using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Managers.Memory;

internal static class FactSources
{
    public static FactSource Parse(string source) => source?.Trim().ToUpperInvariant() switch
    {
        "USER" => FactSource.User,
        "CONTACT" => FactSource.Contact,
        "INFERRED" => FactSource.Inferred,
        "IMPORTED" => FactSource.Imported,
        _ => throw new MemoryException("source must be user (the user said it), contact (they said it), inferred (you worked it out) or imported."),
    };
}
