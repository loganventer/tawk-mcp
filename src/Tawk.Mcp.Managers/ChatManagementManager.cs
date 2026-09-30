using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tawk.Mcp.Core;
using Tawk.Mcp.Engines;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Managers;

public sealed class ChatManagementManager(
    IConfirmationGate gate,
    IChatDirectoryFormatter directory,
    IUntrustedTextFence fence) : IChatManagementManager
{
    public async Task<string> SetChatAsync(
        string chat, string? muted, bool? pinned, bool? archived, bool? locked, WriteContext context, CancellationToken cancellationToken)
    {
        var args = new JsonObject { ["chat"] = chat };
        if (!string.IsNullOrWhiteSpace(muted))
        {
            args["muted"] = ParseMuted(muted);
        }

        if (pinned is { } p)
        {
            args["pinned"] = p;
        }

        if (archived is { } a)
        {
            args["archived"] = a;
        }

        if (locked == true)
        {
            args["locked"] = true;
        }

        var outcome = await RunAsync("set_chat", args, context, cancellationToken).ConfigureAwait(false);
        return WriteResults.Describe(outcome, r =>
            r.ValueKind == JsonValueKind.Object && r.TryGetProperty("chat", out var c)
                ? "Updated.\n" + fence.Wrap("the updated chat", directory.FormatChat(c.Deserialize<ChatSummary>(JsonDefaults.Options)!))
                : "Updated.");
    }

    public async Task<string> SetChatThemeAsync(string chat, string theme, WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(
            await RunAsync("set_chat_theme", new JsonObject { ["chat"] = chat, ["theme"] = theme }, context, cancellationToken).ConfigureAwait(false),
            _ => theme.Length == 0 ? "The chat now uses the app theme." : "Theme set.");

    public async Task<string> ClearChatAsync(string chat, WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(await RunAsync("clear_chat", Chat(chat), context, cancellationToken).ConfigureAwait(false), _ => "Cleared.");

    public async Task<string> DeleteChatAsync(string chat, WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(await RunAsync("delete_chat", Chat(chat), context, cancellationToken).ConfigureAwait(false), _ => "Deleted.");

    public async Task<string> ExportChatAsync(string chat, bool withMedia, WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(
            await RunAsync("export_chat", new JsonObject { ["chat"] = chat, ["with_media"] = withMedia }, context, cancellationToken).ConfigureAwait(false),
            r => $"Exported to {WriteResults.String(r, "path") ?? "your downloads folder"}.");

    public async Task<string> BlockAsync(string chat, WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(await RunAsync("block", Chat(chat), context, cancellationToken).ConfigureAwait(false), _ => "Blocked.");

    public async Task<string> UnblockAsync(string chat, WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(await RunAsync("unblock", Chat(chat), context, cancellationToken).ConfigureAwait(false), _ => "Unblocked.");

    private static JsonObject Chat(string chat) => new() { ["chat"] = chat };

    private static JsonValue ParseMuted(string muted)
    {
        var value = muted.Trim();
        if (bool.TryParse(value, out var flag))
        {
            return JsonValue.Create(flag);
        }

        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            ? JsonValue.Create(seconds)
            : throw new TawkControlException(ControlErrorCode.BadRequest, "muted must be true, false or a number of seconds.");
    }

    private Task<ConfirmationOutcome> RunAsync(string op, JsonObject args, WriteContext context, CancellationToken cancellationToken) =>
        gate.ExecuteAsync(op, args, context.Confirmation, context.OnApprovalWaiting, cancellationToken);
}
