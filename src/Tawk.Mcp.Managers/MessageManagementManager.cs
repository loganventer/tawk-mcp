using System.Globalization;
using System.Text.Json.Nodes;
using Tawk.Mcp.Core;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Managers;

public sealed class MessageManagementManager(IConfirmationGate gate) : IMessageManagementManager
{
    public async Task<string> EditMessageAsync(string messageId, string text, WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(
            await RunAsync("edit_message", new JsonObject { ["message_id"] = messageId, ["text"] = text }, context, cancellationToken).ConfigureAwait(false),
            _ => "Edited.");

    public async Task<string> DeleteMessageAsync(string messageId, bool forEveryone, WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(
            await RunAsync("delete_message", new JsonObject { ["message_id"] = messageId, ["for_everyone"] = forEveryone }, context, cancellationToken).ConfigureAwait(false),
            _ => "Deleted.");

    public async Task<string> ForwardMessageAsync(string messageId, IReadOnlyList<string> chats, WriteContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chats);
        var list = new JsonArray([.. chats.Select(c => (JsonNode?)JsonValue.Create(c))]);
        var outcome = await RunAsync("forward_message", new JsonObject { ["message_id"] = messageId, ["chats"] = list }, context, cancellationToken)
            .ConfigureAwait(false);
        return WriteResults.Describe(outcome, r => string.Create(
            CultureInfo.InvariantCulture, $"Forwarded to {WriteResults.Number(r, "forwarded") ?? chats.Count} chats."));
    }

    public async Task<string> RetryMessageAsync(string messageId, WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(
            await RunAsync("retry_message", new JsonObject { ["message_id"] = messageId }, context, cancellationToken).ConfigureAwait(false),
            _ => "tawk is sending the message again.");

    public async Task<string> DownloadMediaAsync(string messageId, WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(
            await RunAsync("download_media", new JsonObject { ["message_id"] = messageId }, context, cancellationToken).ConfigureAwait(false),
            _ => "tawk is downloading the file in the background.");

    private Task<ConfirmationOutcome> RunAsync(string op, JsonObject args, WriteContext context, CancellationToken cancellationToken) =>
        gate.ExecuteAsync(op, args, context.Confirmation, context.OnApprovalWaiting, cancellationToken);
}
