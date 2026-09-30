using System.Text;
using System.Text.RegularExpressions;

namespace Tawk.Mcp.Engines.Memory;

public sealed partial class VoiceGuideImporter : IVoiceGuideImporter
{
    public ImportedVoiceGuide Split(string markdown, IReadOnlyDictionary<string, string> sections)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(sections);
        var wanted = sections.ToDictionary(p => Normalise(p.Key), p => p.Value, StringComparer.Ordinal);
        var found = new List<ImportedSection>();
        var basis = new StringBuilder();
        var lines = markdown.ReplaceLineEndings("\n").Split('\n');

        string? heading = null;
        string? category = null;
        var level = 0;
        var body = new StringBuilder();
        foreach (var line in lines)
        {
            var match = Heading().Match(line);
            if (match.Success && category is not null && match.Groups[1].Length <= level)
            {
                found.Add(new ImportedSection(heading!, category, body.ToString().Trim()));
                category = null;
                body.Clear();
            }

            if (match.Success && category is null && wanted.TryGetValue(Normalise(match.Groups[2].Value), out var target))
            {
                heading = match.Groups[2].Value.Trim();
                category = target;
                level = match.Groups[1].Length;
                continue;
            }

            (category is null ? basis : body).Append(line).Append('\n');
        }

        if (category is not null)
        {
            found.Add(new ImportedSection(heading!, category, body.ToString().Trim()));
        }

        var matched = found.Select(s => Normalise(s.Heading)).ToHashSet(StringComparer.Ordinal);
        var unmatched = sections.Keys.Where(k => !matched.Contains(Normalise(k))).ToList();
        return new ImportedVoiceGuide(basis.ToString().Trim(), found, unmatched);
    }

    private static string Normalise(string heading) => Spaces().Replace(heading.Trim().ToUpperInvariant(), " ");

    [GeneratedRegex(@"^(#{1,6})\s+(.+?)\s*#*\s*$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
