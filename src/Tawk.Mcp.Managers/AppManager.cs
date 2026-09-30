using Tawk.Mcp.Core;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Managers;

public sealed class AppManager(ITawkControl control, IConfirmationGate gate) : IAppManager
{
    public async Task<string> AppStatusAsync(CancellationToken cancellationToken) =>
        WriteResults.Json(await control.RequestAsync("app_status", null, cancellationToken).ConfigureAwait(false));

    public async Task<string> ReconnectAsync(WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(
            await gate.ExecuteAsync("reconnect", null, context.Confirmation, context.OnApprovalWaiting, cancellationToken).ConfigureAwait(false),
            _ => "tawk is reconnecting to WhatsApp.");

    public async Task<string> DeclineCallAsync(WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(
            await gate.ExecuteAsync("decline_call", null, context.Confirmation, context.OnApprovalWaiting, cancellationToken).ConfigureAwait(false),
            _ => "Declined the call.");
}
