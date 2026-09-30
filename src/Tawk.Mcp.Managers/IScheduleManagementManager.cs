using Tawk.Mcp.Core;

namespace Tawk.Mcp.Managers;

public interface IScheduleManagementManager
{
    Task<string> CancelScheduledAsync(string id, WriteContext context, CancellationToken cancellationToken);

    Task<string> RescheduleAsync(string id, string dueAt, WriteContext context, CancellationToken cancellationToken);

    Task<string> SendScheduledNowAsync(string id, WriteContext context, CancellationToken cancellationToken);
}
