namespace Tawk.Mcp.Core.Sync;

public enum SyncOutcome
{
    Disabled,
    Busy,
    UpToDate,
    Pulled,
    Pushed,
    PulledAndPushed,
    Failed,
}
