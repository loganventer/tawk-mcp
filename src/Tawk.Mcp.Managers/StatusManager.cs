using System.Text.Json.Nodes;
using Tawk.Mcp.Core;
using Tawk.Mcp.Engines;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Managers;

public sealed class StatusManager(ITawkControl control, IConfirmationGate gate, IUntrustedTextFence fence) : IStatusManager
{
    public async Task<string> StatusViewersAsync(string statusId, CancellationToken cancellationToken)
    {
        var result = await control.RequestAsync("status_viewers", new JsonObject { ["status_id"] = statusId }, cancellationToken)
            .ConfigureAwait(false);
        return fence.Wrap("the viewers of a status, as JSON", WriteResults.Json(result));
    }

    public async Task<string> ListBackgroundsAsync(CancellationToken cancellationToken) =>
        WriteResults.Json(await control.RequestAsync("list_backgrounds", null, cancellationToken).ConfigureAwait(false));

    public async Task<string> PostStatusAsync(
        string kind, string? text, string? file, string? background, WriteContext context, CancellationToken cancellationToken)
    {
        var args = new JsonObject { ["kind"] = kind };
        Add(args, "text", text);
        Add(args, "file", file);
        Add(args, "background", background);
        return WriteResults.Describe(
            await RunAsync("post_status", args, context, cancellationToken).ConfigureAwait(false),
            _ => "tawk is posting the status and shows when it is done.");
    }

    public async Task<string> ReplyStatusAsync(string statusId, string text, WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(
            await RunAsync("reply_status", new JsonObject { ["status_id"] = statusId, ["text"] = text }, context, cancellationToken).ConfigureAwait(false),
            r => $"Replied in your chat with its author (message id {WriteResults.String(r, "id") ?? "unknown"}).");

    public async Task<string> LikeStatusAsync(string statusId, WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(
            await RunAsync("like_status", new JsonObject { ["status_id"] = statusId }, context, cancellationToken).ConfigureAwait(false),
            r => WriteResults.String(r, "how") == "reply" ? "Sent a heart reply to its author." : "Liked.");

    private static void Add(JsonObject args, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            args[name] = value;
        }
    }

    private Task<ConfirmationOutcome> RunAsync(string op, JsonObject args, WriteContext context, CancellationToken cancellationToken) =>
        gate.ExecuteAsync(op, args, context.Confirmation, context.OnApprovalWaiting, cancellationToken);
}
