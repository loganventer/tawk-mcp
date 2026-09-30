namespace Tawk.Mcp.ResourceAccess.Memory;

/// <summary>Escapes text for a LIKE pattern that uses a backslash as its escape character.</summary>
internal static class Like
{
    public static string Escape(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
