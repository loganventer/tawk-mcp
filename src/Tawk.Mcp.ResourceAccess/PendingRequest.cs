using System.Text.Json;

namespace Tawk.Mcp.ResourceAccess;

internal sealed class PendingRequest(Action? onApprovalWaiting)
{
    private int _approvalReported;

    public TaskCompletionSource<JsonElement> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool ApprovalReported => Volatile.Read(ref _approvalReported) == 1;

    public void ReportApprovalWaiting()
    {
        if (Interlocked.Exchange(ref _approvalReported, 1) == 0)
        {
            onApprovalWaiting?.Invoke();
        }
    }
}
