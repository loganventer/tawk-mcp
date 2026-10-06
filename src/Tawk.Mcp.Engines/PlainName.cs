using System.Text.RegularExpressions;

namespace Tawk.Mcp.Engines;

/// <summary>Names are chosen by other people too, so they are shortened and kept to one plain line.</summary>
internal static partial class PlainName
{
    private const int MaxLength = 60;

    public static string Of(string name)
    {
        var flat = Unsafe().Replace(name.ReplaceLineEndings(" "), " ").Trim();
        return flat.Length <= MaxLength ? flat : string.Concat(flat.AsSpan(0, MaxLength - 3), "...");
    }

    [GeneratedRegex("[<>\"`]+")]
    private static partial Regex Unsafe();
}
