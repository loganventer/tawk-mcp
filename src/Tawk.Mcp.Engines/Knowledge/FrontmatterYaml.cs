using System.Globalization;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Tawk.Mcp.Engines.Knowledge;

/// <summary>Reads a YAML frontmatter block into a JSON tree, so the rest of the reader deals with one shape.</summary>
internal static class FrontmatterYaml
{
    /// <summary>The mapping at the top of the block, or null when it is not one or does not parse.</summary>
    public static JsonObject? Parse(string yaml)
    {
        try
        {
            var stream = new YamlStream();
            using var reader = new StringReader(yaml);
            stream.Load(reader);
            return stream.Documents.Count > 0 && stream.Documents[0].RootNode is YamlMappingNode root ? (JsonObject)Convert(root)! : null;
        }
        catch (YamlException)
        {
            return null;
        }
    }

    private static JsonNode? Convert(YamlNode node)
    {
        switch (node)
        {
            case YamlMappingNode map:
                var result = new JsonObject();
                foreach (var (key, value) in map.Children)
                {
                    result[(key as YamlScalarNode)?.Value ?? key.ToString()] = Convert(value);
                }

                return result;
            case YamlSequenceNode list:
                var items = new JsonArray();
                foreach (var item in list.Children)
                {
                    items.Add(Convert(item));
                }

                return items;
            case YamlScalarNode scalar:
                return scalar.Style == ScalarStyle.Plain ? Plain(scalar.Value ?? string.Empty) : JsonValue.Create(scalar.Value ?? string.Empty);
            default:
                return JsonValue.Create(node.ToString());
        }
    }

    private static JsonValue? Plain(string text)
    {
        if (text.Length == 0 || text == "~" || text.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (text.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return JsonValue.Create(true);
        }

        if (text.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return JsonValue.Create(false);
        }

        if (long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole))
        {
            return JsonValue.Create(whole);
        }

        return (char.IsAsciiDigit(text[0]) || text[0] is '-' or '.')
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
            ? JsonValue.Create(number)
            : JsonValue.Create(text);
    }
}
