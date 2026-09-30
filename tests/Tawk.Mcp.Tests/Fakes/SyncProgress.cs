using ModelContextProtocol;

namespace Tawk.Mcp.Tests.Fakes;

public sealed class SyncProgress(List<ProgressNotificationValue> reports) : IProgress<ProgressNotificationValue>
{
    public void Report(ProgressNotificationValue value)
    {
        lock (reports)
        {
            reports.Add(value);
        }
    }
}
