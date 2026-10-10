using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Media;

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
            var decoded = DecodeEvent(evt.GetString()!, root);
            return new ControlEventFrame(
                root.TryGetProperty("account", out var account) && account.ValueKind == JsonValueKind.Object
                    ? decoded with { Account = Deserialize<AccountRef>(account) }
                    : decoded);
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
            Property<ChatMessage>(root, "message")) { Transcribe = MayTranscribe(root), Languages = SpokenIn(root) },
        "read" => new ReadEvent(
            Property<ChatRef>(root, "chat"),
            root.TryGetProperty("message_id", out var read) ? read.GetString() ?? string.Empty : string.Empty,
            Property<ReaderRef>(root, "reader"),
            root.TryGetProperty("at", out var at) && at.TryGetInt64(out var seconds) ? seconds : 0),
        "presence" => new PresenceEvent(
            Property<ChatRef>(root, "chat"),
            Property<ReaderRef>(root, "who"),
            root.TryGetProperty("state", out var presence) && presence.GetString() == "online",
            root.TryGetProperty("last_seen", out var seen) && seen.TryGetInt64(out var lastSeen) && lastSeen > 0 ? lastSeen : null,
            root.TryGetProperty("at", out var heard) && heard.TryGetInt64(out var heardAt) ? heardAt : 0),
        "reaction" => Activity(ActivityKind.Reaction, root),
        "edit" => Activity(ActivityKind.Edited, root),
        "delete" => Activity(ActivityKind.Deleted, root),
        "scheduled_sent" => Activity(ActivityKind.ScheduledSent, root),
        "chat" => new ChatUpdatedEvent(Property<ChatSummary>(root, "chat")),
        "media_ready" => new MediaReadyEvent(
            root.TryGetProperty("chat", out var mediaChat) && mediaChat.ValueKind == JsonValueKind.Object ? Deserialize<ChatRef>(mediaChat) : null,
            root.TryGetProperty("message_id", out var mediaId) ? mediaId.GetString() ?? string.Empty : string.Empty,
            root.TryGetProperty("path", out var mediaPath) ? mediaPath.GetString() ?? string.Empty : string.Empty,
            root.TryGetProperty("type", out var mediaType) ? mediaType.GetString() : null) { Transcribe = MayTranscribe(root) },
        "transcript_wanted" => new Core.Transcription.TranscriptWantedEvent(
            root.TryGetProperty("chat", out var spokenChat) && spokenChat.ValueKind == JsonValueKind.Object ? Deserialize<ChatRef>(spokenChat) : null,
            root.TryGetProperty("message_id", out var spokenId) ? spokenId.GetString() ?? string.Empty : string.Empty) { Languages = SpokenIn(root) },
        "summary_wanted" => new SummaryWantedEvent(
            Property<ChatRef>(root, "chat"),
            Property<ChatMessage>(root, "message"),
            root.TryGetProperty("max_chars", out var most) && most.TryGetInt32(out var chars) && chars > 0 ? chars : 400),
        "bye" => new ByeEvent(),
        "approval" => new ApprovalEvent(
            root.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
            root.TryGetProperty("state", out var state) ? state.GetString() ?? string.Empty : string.Empty),
        _ => new UnknownEvent(name),
    };

    /// <summary>tawk adds "transcribe":false to what it says about a chat whose voice notes are not to be transcribed.</summary>
    public static bool MayTranscribe(JsonElement root) =>
        root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("transcribe", out var allowed) || allowed.ValueKind != JsonValueKind.False;

    /// <summary>The "languages" tawk adds for a chat whose voice note languages the user named; null when there are none.</summary>
    public static IReadOnlyList<string>? SpokenIn(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("languages", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var codes = list.EnumerateArray()
            .Where(code => code.ValueKind == JsonValueKind.String)
            .Select(code => code.GetString()!)
            .Where(code => code.Length > 0)
            .ToList();
        return codes.Count == 0 ? null : codes;
    }

    private static MessageActivityEvent Activity(ActivityKind kind, JsonElement root) => new(
        kind,
        Property<ChatRef>(root, "chat"),
        root.TryGetProperty("message_id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
        root.TryGetProperty("who", out var who) ? Deserialize<ReaderRef>(who) : null,
        root.TryGetProperty("emoji", out var emoji) ? emoji.GetString() : null,
        root.TryGetProperty("message", out var message) ? Deserialize<ChatMessage>(message) : null,
        root.TryGetProperty("at", out var at) && at.TryGetInt64(out var seconds) ? seconds : 0);

    private static T Property<T>(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value)
            ? Deserialize<T>(value)
            : throw new JsonException($"The {name} field is missing.");
}
