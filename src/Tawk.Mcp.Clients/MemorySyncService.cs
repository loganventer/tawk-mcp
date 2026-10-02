using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Tawk.Mcp.Core.Sync;
using Tawk.Mcp.Managers.Sync;

namespace Tawk.Mcp.Clients;

/// <summary>Syncs memory when the server starts and then once every interval, for the life of the process.</summary>
public sealed partial class MemorySyncService(
    IMemorySyncManager sync, SyncOptions options, TimeProvider clock, ILogger<MemorySyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            return;
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var report = await sync.SyncAsync(stoppingToken).ConfigureAwait(false);
                if (report.Outcome == SyncOutcome.Failed)
                {
                    LogFailed(report.Message);
                }
                else
                {
                    LogSynced(report.Message);
                }

                await Task.Delay(options.Interval, clock, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Report}")]
    private partial void LogSynced(string report);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Report}")]
    private partial void LogFailed(string report);
}
