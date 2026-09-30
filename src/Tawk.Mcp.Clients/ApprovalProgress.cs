using ModelContextProtocol;

namespace Tawk.Mcp.Clients;

public static class ApprovalProgress
{
    public const string WaitingMessage = "Waiting for approval in tawk";

    public static Action Reporter(IProgress<ProgressNotificationValue>? progress) =>
        () => progress?.Report(new ProgressNotificationValue { Progress = 0, Message = WaitingMessage });
}
