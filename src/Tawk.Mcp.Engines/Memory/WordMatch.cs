using System.Text.RegularExpressions;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>Finds a whole word or phrase, ignoring case, so "jy" does not match "jammer".</summary>
internal static class WordMatch
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);

    public static bool Contains(string text, string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return false;
        }

        var pattern = @"(?<![\p{L}\p{N}])" + Regex.Escape(phrase.Trim()) + @"(?![\p{L}\p{N}])";
        return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout);
    }
}
