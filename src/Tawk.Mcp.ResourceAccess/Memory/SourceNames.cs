using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.ResourceAccess.Memory;

/// <summary>Stores fact sources as lowercase words, so the database reads well and survives enum reordering.</summary>
internal static class SourceNames
{
    public static string ToName(FactSource source) => source switch
    {
        FactSource.User => "user",
        FactSource.Contact => "contact",
        FactSource.Imported => "imported",
        _ => "inferred",
    };

    public static FactSource FromName(string name) => name switch
    {
        "user" => FactSource.User,
        "contact" => FactSource.Contact,
        "imported" => FactSource.Imported,
        _ => FactSource.Inferred,
    };
}
