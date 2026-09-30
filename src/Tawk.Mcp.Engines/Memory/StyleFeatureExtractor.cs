using System.Globalization;
using System.Text.RegularExpressions;

namespace Tawk.Mcp.Engines.Memory;

public sealed partial class StyleFeatureExtractor : IStyleFeatureExtractor
{
    // Common short words that tell Afrikaans and English apart in chat-length text.
    private static readonly HashSet<string> Afrikaans = new(StringComparer.Ordinal)
    {
        "ek", "jy", "nie", "die", "en", "van", "het", "dit", "sal", "vir", "wat", "maar", "ons", "hoor", "asb", "dankie",
        "lekker", "baie", "om", "te", "met", "ook", "nou", "gaan", "kan", "moet", "jou", "julle", "hulle",
        "sy", "hy", "ja", "nee", "op", "pad", "toe", "nog", "weer", "gou", "sommer", "darem", "vandag", "dag", "net", "al", "daar", "hier", "ne", "nê", "jammer", "môre", "more", "seker", "dis", "n", "'n",
    };

    private static readonly HashSet<string> English = new(StringComparer.Ordinal)
    {
        "the", "and", "i", "you", "to", "of", "it", "that", "for", "not", "will", "have", "what", "but", "we", "thanks",
        "please", "this", "just", "can", "be", "are", "do", "don't", "so", "my", "your", "with", "on", "at", "good", "yes",
        "no", "would", "should", "i'm", "it's", "going", "there", "they", "if", "a",
    };

    private static readonly string[] Emoticons = [":)", ":-)", ":(", ":D", ":P", ":p", ";)", ";-)", ":/", "xD"];

    public StyleFeatures Extract(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = text.Trim();
        var words = WordPattern().Matches(trimmed).Select(m => m.Value.ToLowerInvariant()).ToList();
        var letters = trimmed.Where(char.IsLetter).ToList();
        var first = letters.Count == 0 ? '\0' : letters[0];
        var lines = trimmed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new StyleFeatures(
            trimmed,
            words,
            char.IsUpper(first),
            char.IsLower(first),
            letters.Count > 0 && letters.All(c => !char.IsUpper(c)),
            FindEmoji(trimmed),
            EllipsisPattern().Count(trimmed),
            trimmed.Contains('—', StringComparison.Ordinal),
            LaughPattern().IsMatch(trimmed),
            lines.Length == 0 ? string.Empty : lines[0],
            lines.Length == 0 ? string.Empty : lines[^1],
            GuessLanguage(words));
    }

    private static List<string> FindEmoji(string text)
    {
        var found = new List<string>();
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            if (IsEmoji(element))
            {
                found.Add(element);
            }
        }

        foreach (var emoticon in Emoticons)
        {
            var at = text.IndexOf(emoticon, StringComparison.Ordinal);
            while (at >= 0)
            {
                found.Add(emoticon);
                at = text.IndexOf(emoticon, at + emoticon.Length, StringComparison.Ordinal);
            }
        }

        return found;
    }

    private static bool IsEmoji(string element)
    {
        var rune = element.EnumerateRunes().FirstOrDefault();
        var value = rune.Value;
        return value is >= 0x1F000 and <= 0x1FAFF
            or >= 0x2600 and <= 0x27BF
            or >= 0x2300 and <= 0x23FF
            or >= 0x2B00 and <= 0x2BFF
            || element.Contains('️', StringComparison.Ordinal);
    }

    private static string GuessLanguage(List<string> words)
    {
        var af = words.Count(Afrikaans.Contains);
        var en = words.Count(English.Contains);
        if (af + en < 2)
        {
            return "unknown";
        }

        if (af > 0 && en > 0 && Math.Min(af, en) * 3 >= Math.Max(af, en))
        {
            return "mix";
        }

        return af > en ? "af" : "en";
    }

    [GeneratedRegex(@"[\p{L}\p{N}][\p{L}\p{N}'’]*")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"\.\.\.|…")]
    private static partial Regex EllipsisPattern();

    [GeneratedRegex(@"\b(lol|haha\w*|hehe\w*|lmao)\b|😂|🤣", RegexOptions.IgnoreCase)]
    private static partial Regex LaughPattern();
}
