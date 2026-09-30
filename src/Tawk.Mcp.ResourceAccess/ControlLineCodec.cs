using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.ResourceAccess;

public sealed class ControlLineCodec
{
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = false,
    };

    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    public static T Deserialize<T>(JsonElement element) =>
        element.Deserialize<T>(SerializerOptions)
        ?? throw new TawkControlException(ControlErrorCode.Failed, $"tawk sent an empty {typeof(T).Name}.");

    public string EncodeRequest(string id, string op, JsonObject? args)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(op);
        var request = new JsonObject
        {
            ["id"] = id,
            ["op"] = op,
        };
        if (args is not null)
        {
            request["args"] = args.DeepClone();
        }

        return request.ToJsonString(SerializerOptions);
    }

    /// <summary>Decodes one line, or returns null when the line is neither an answer nor a notification.</summary>
    public ControlFrame? Decode(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (root.TryGetProperty("evt", out var evt) && evt.ValueKind == JsonValueKind.String)
        {
            return new ControlEventFrame(DecodeEvent(evt.GetString()!, root));
        }

        if (!root.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var ok = root.TryGetProperty("ok", out var okElement) && okElement.ValueKind == JsonValueKind.True;
        if (ok)
        {
            var result = root.TryGetProperty("result", out var r) ? r.Clone() : EmptyObject;
            return new ControlResponse(id.GetString()!, true, result, null);
        }

        var error = root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.Object
            ? e.Deserialize<ControlError>(SerializerOptions)
            : null;
        error ??= new ControlError("failed", "tawk refused the request without saying why.");
        return new ControlResponse(id.GetString()!, false, EmptyObject, error);
    }

    private static TawkEvent DecodeEvent(string name, JsonElement root) => name switch
    {
        "message" => new MessageEvent(
            Property<ChatRef>(root, "chat"),
            Property<ChatMessage>(root, "message")),
        "chat" => new ChatUpdatedEvent(Property<ChatSummary>(root, "chat")),
        "bye" => new ByeEvent(),
        "approval" => new ApprovalEvent(
            root.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
            root.TryGetProperty("state", out var state) ? state.GetString() ?? string.Empty : string.Empty),
        _ => new UnknownEvent(name),
    };

    private static T Property<T>(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value)
            ? Deserialize<T>(value)
            : throw new JsonException($"The {name} field is missing.");
}
