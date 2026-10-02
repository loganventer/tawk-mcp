using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Tawk.Mcp.Engines.Knowledge;

/// <summary>Writes a JSON tree as block YAML. Strings that could be misread are double-quoted.</summary>
internal static partial class YamlText
{
    private static readonly JsonSerializerOptions Quoted = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly HashSet<string> Words = new(StringComparer.OrdinalIgnoreCase) { "true", "false", "null", "yes", "no", "on", "off", "y", "n" };

    public static void Write(StringBuilder text, JsonObject map, int indent)
    {
        foreach (var (key, value) in map)
        {
            text.Append(' ', indent).Append(Key().IsMatch(key) ? key : Quote(key)).Append(':');
            switch (value)
            {
                case JsonObject child when child.Count > 0:
                    text.Append('\n');
                    Write(text, child, indent + 2);
                    break;
                case JsonArray list when list.Count > 0 && list.All(item => item is null or JsonValue):
                    text.Append(" [").Append(string.Join(", ", list.Select(Scalar))).Append("]\n");
                    break;
                case JsonArray list when list.Count > 0:
                    text.Append('\n');
                    foreach (var item in list)
                    {
                        WriteItem(text, item, indent + 2);
                    }

                    break;
                case JsonObject:
                    text.Append(" {}\n");
                    break;
                case JsonArray:
                    text.Append(" []\n");
                    break;
                default:
                    text.Append(' ').Append(Scalar(value)).Append('\n');
                    break;
            }
        }
    }

    private static void WriteItem(StringBuilder text, JsonNode? item, int indent)
    {
        text.Append(' ', indent).Append("- ");
        if (item is JsonObject map && map.Count > 0)
        {
            // The first key shares the dash's line; the rest line up under it.
            var block = new StringBuilder();
            Write(block, map, indent + 2);
            text.Append(block, indent + 2, block.Length - indent - 2);
        }
        else if (item is JsonObject or JsonArray)
        {
            // JSON is valid flow YAML.
            text.Append(item.ToJsonString(Quoted)).Append('\n');
        }
        else
        {
            text.Append(Scalar(item)).Append('\n');
        }
    }

    private static string Scalar(JsonNode? value)
    {
        if (value is null)
        {
            return "null";
        }

        return value.GetValueKind() == JsonValueKind.String ? Text(value.GetValue<string>()) : value.ToJsonString();
    }

    private static string Text(string value) =>
        Timestamp().IsMatch(value) || (Plain().IsMatch(value) && !Words.Contains(value)) ? value : Quote(value);

    private static string Quote(string value) => JsonSerializer.Serialize(value, Quoted);

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_-]*$")]
    private static partial Regex Key();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_./@-]*( [A-Za-z0-9_./@-]+)*$")]
    private static partial Regex Plain();

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$")]
    private static partial Regex Timestamp();
}
