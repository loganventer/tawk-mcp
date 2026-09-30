using System.Text.Json;

namespace Tawk.Mcp.Managers;

internal static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
}
