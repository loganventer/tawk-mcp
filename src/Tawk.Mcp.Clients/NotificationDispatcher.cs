using Microsoft.Extensions.Hosting;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Clients;

/// <summary>Runs live updates for the life of the process: tawk events go to every sink.</summary>
public sealed class NotificationDispatcher(ILiveUpdatesManager liveUpdates) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await liveUpdates.RunAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }
}
