using System.Text.Json;

namespace Tawk.Mcp.ResourceAccess.Memory;

/// <summary>How structured columns are written: snake_case JSON, nulls left out.</summary>
internal static class StoreJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Read<T>(string json, T fallback) => JsonSerializer.Deserialize<T>(json, Options) ?? fallback;
}
