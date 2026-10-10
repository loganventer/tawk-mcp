using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Core.Sync;
using Tawk.Mcp.Managers.Sync;

namespace Tawk.Mcp.Clients.Tools;

[McpServerToolType]
public sealed class MemorySyncTools(IMemorySyncManager sync)
{
    [McpServerTool(Name = "sync_memory", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = true)]
    [Description(
        "Sync tawk-mcp's memory (voices, contact profiles, knowledge, templates) with the user's own private repository once, now: what the "
        + "other machines wrote is merged in, and what this one holds that the repository lacks is pushed. It runs by itself every so often "
        + "when a repository is set; use this when the user asks to sync now. The repository and its key are the user's own, set where "
        + "tawk-mcp was started, and you cannot set or change them: with none set, this says so and does nothing. Nothing is sent to WhatsApp.")]
    public async Task<CallToolResult> SyncMemoryAsync(CancellationToken cancellationToken = default)
    {
        var report = await sync.SyncAsync(cancellationToken).ConfigureAwait(false);
        return report.Outcome == SyncOutcome.Failed ? ToolResults.Error(report.Message) : ToolResults.Text(report.Message);
    }
}
