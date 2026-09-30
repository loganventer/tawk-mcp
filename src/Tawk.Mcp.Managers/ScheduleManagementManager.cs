using System.Text.Json.Nodes;
using Tawk.Mcp.Core;
using Tawk.Mcp.Engines;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Managers;

public sealed class ScheduleManagementManager(IConfirmationGate gate, ITranscriptFormatter transcript, IScheduleJitter jitter) : IScheduleManagementManager
{
    public async Task<string> CancelScheduledAsync(string id, WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(await RunAsync("cancel_scheduled", Id(id), context, cancellationToken).ConfigureAwait(false), _ => "Cancelled.");

    public async Task<string> RescheduleAsync(string id, string dueAt, WriteContext context, CancellationToken cancellationToken)
    {
        var nudge = jitter.Nudge(dueAt);
        ConfirmationOutcome outcome;
        try
        {
            outcome = await RunAsync("reschedule", new JsonObject { ["id"] = id, ["when"] = nudge.When }, context, cancellationToken).ConfigureAwait(false);
        }
        catch (TawkControlException ex) when (ex.Code == ControlErrorCode.BadRequest && nudge.When != dueAt)
        {
            // An older tawk cannot read the seconds adjustment. It refuses before asking the user, so asking again costs nothing.
            nudge = new ScheduleNudge(dueAt, TimeSpan.Zero);
            outcome = await RunAsync("reschedule", new JsonObject { ["id"] = id, ["when"] = dueAt }, context, cancellationToken).ConfigureAwait(false);
        }

        return WriteResults.Describe(
            outcome,
            r => (WriteResults.Number(r, "due_at") is { } due ? $"Rescheduled for {transcript.FormatTimestamp(due)}." : "Rescheduled.")
                + ScheduleNudges.Describe(nudge));
    }

    public async Task<string> SendScheduledNowAsync(string id, WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(await RunAsync("send_scheduled_now", Id(id), context, cancellationToken).ConfigureAwait(false), _ => "Sending now.");

    private static JsonObject Id(string id) => new() { ["id"] = id };

    private Task<ConfirmationOutcome> RunAsync(string op, JsonObject args, WriteContext context, CancellationToken cancellationToken) =>
        gate.ExecuteAsync(op, args, context.Confirmation, context.OnApprovalWaiting, cancellationToken);
}
