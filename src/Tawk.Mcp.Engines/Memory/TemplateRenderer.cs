using System.Text.RegularExpressions;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>Fills {{name}} and {{contact.field}} placeholders. Never guesses: anything without a value is reported.</summary>
public sealed partial class TemplateRenderer : ITemplateRenderer
{
    public IReadOnlyList<string> Placeholders(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return Placeholder().Matches(body).Select(m => m.Groups[1].Value.ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToList();
    }

    public TemplateRendering Render(string body, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(values);
        var missing = new List<string>();
        var text = Placeholder().Replace(body, match =>
        {
            var name = match.Groups[1].Value.ToLowerInvariant();
            if (values.TryGetValue(name, out var value))
            {
                return value;
            }

            if (!missing.Contains(name, StringComparer.Ordinal))
            {
                missing.Add(name);
            }

            return match.Value;
        });
        return new TemplateRendering(text, missing);
    }

    [GeneratedRegex(@"\{\{\s*([A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)?)\s*\}\}")]
    private static partial Regex Placeholder();
}
