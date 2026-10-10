using Tawk.Mcp.Core.Transcription;

namespace Tawk.Mcp.Engines.Transcription;

/// <summary>
/// A voice note is written out in the language spoken, never translated. The engine's own guess leans
/// towards English, and told that speech is English it writes an English translation of whatever it
/// hears. So English is chosen only when no other language is heard with any weight: a note in
/// Afrikaans with English mixed in is written in Afrikaans, where the English words stay as they were said.
/// </summary>
public sealed class SpokenLanguageRule : ISpokenLanguageRule
{
    public const string English = "en";

    /// <summary>How sure the engine must be of another language, in any stretch of the note, for it to win over English.</summary>
    public const float OtherLanguageWins = 0.10f;

    public string? Choose(IReadOnlyList<LanguageReading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);
        if (readings.Count == 0)
        {
            return null;
        }

        // How strongly each language other than English was heard, over every stretch.
        var heard = new Dictionary<string, (float Most, float Sum)>(StringComparer.Ordinal);
        foreach (var reading in readings)
        {
            var (language, probability) = reading.Top is { } top && !IsEnglish(top)
                ? (top, reading.TopProbability)
                : (reading.OtherThanEnglish, reading.OtherProbability);
            if (language is null || IsEnglish(language))
            {
                continue;
            }

            var before = heard.GetValueOrDefault(language);
            heard[language] = (Math.Max(before.Most, probability), before.Sum + probability);
        }

        if (heard.Count > 0)
        {
            var strongest = heard.OrderByDescending(h => h.Value.Sum).ThenBy(h => h.Key, StringComparer.Ordinal).First();
            if (strongest.Value.Most >= OtherLanguageWins)
            {
                return strongest.Key;
            }
        }

        return readings.Any(r => r.Top is { } top && IsEnglish(top)) ? English : readings[0].Top;
    }

    private static bool IsEnglish(string language) => string.Equals(language, English, StringComparison.OrdinalIgnoreCase);
}
