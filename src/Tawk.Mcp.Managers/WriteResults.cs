using System.Text.Encodings.Web;
using System.Text.Json;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.Managers;

internal static class WriteResults
{
    public const string CannotAsk =
        "This needs your confirmation, and your MCP client cannot ask you. Do it in tawk instead.";

    public const string Declined = "You declined, so nothing was done.";

    private static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string Describe(ConfirmationOutcome outcome, Func<JsonElement, string> done) => outcome.Status switch
    {
        ConfirmationStatus.CannotAsk => CannotAsk,
        ConfirmationStatus.DeclinedByUser => Declined,
        _ => outcome.Summary is null ? done(outcome.Result) : $"Done after you confirmed it in your client and in tawk: {outcome.Summary}",
    };

    public static string Json(JsonElement element) => JsonSerializer.Serialize(element, Compact);

    public static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    public static long? Number(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.TryGetInt64(out var n)
            ? n
            : null;
}
