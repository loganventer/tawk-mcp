using System.Text.Json;
using System.Text.Json.Serialization;
using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>Reads and writes voice rules as snake_case JSON, refusing unknown keys so typos are caught.</summary>
public static class RulesJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static string Write(VoiceRules rules) => JsonSerializer.Serialize(rules, Options);

    /// <summary>Parses rules, or returns null with the reason in <paramref name="problem"/>.</summary>
    public static VoiceRules? Read(string json, out string? problem)
    {
        try
        {
            problem = null;
            return JsonSerializer.Deserialize<VoiceRules>(json, Options) ?? VoiceRules.None;
        }
        catch (JsonException ex)
        {
            problem = ex.Message;
            return null;
        }
    }
}
