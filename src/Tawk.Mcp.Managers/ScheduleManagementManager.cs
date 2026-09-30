using System.Text.Json.Nodes;
using Tawk.Mcp.Core;
using Tawk.Mcp.Engines;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Managers;

public sealed class ScheduleManagementManager(IConfirmationGate gate, ITranscriptFormatter transcript) : IScheduleManagementManager
{
    public async Task<string> CancelScheduledAsync(string id, WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(await RunAsync("cancel_scheduled", Id(id), context, cancellationToken).ConfigureAwait(false), _ => "Cancelled.");

    public async Task<string> RescheduleAsync(string id, string dueAt, WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(
            await RunAsync("reschedule", new JsonObject { ["id"] = id, ["when"] = dueAt }, context, cancellationToken).ConfigureAwait(false),
            r => WriteResults.Number(r, "due_at") is { } due ? $"Rescheduled for {transcript.FormatTimestamp(due)}." : "Rescheduled.");

    public async Task<string> SendScheduledNowAsync(string id, WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(await RunAsync("send_scheduled_now", Id(id), context, cancellationToken).ConfigureAwait(false), _ => "Sending now.");

    private static JsonObject Id(string id) => new() { ["id"] = id };

    private Task<ConfirmationOutcome> RunAsync(string op, JsonObject args, WriteContext context, CancellationToken cancellationToken) =>
        gate.ExecuteAsync(op, args, context.Confirmation, context.OnApprovalWaiting, cancellationToken);
}
