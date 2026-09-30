using ModelContextProtocol;

namespace Tawk.Mcp.Tests.Fakes;

public sealed class FakeProgress : IProgress<ProgressNotificationValue>
{
    public List<ProgressNotificationValue> Reports { get; } = [];

    public void Report(ProgressNotificationValue value) => Reports.Add(value);
}
