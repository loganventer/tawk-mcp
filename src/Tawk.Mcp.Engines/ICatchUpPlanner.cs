using Tawk.Mcp.Core;

namespace Tawk.Mcp.Engines;

public interface ICatchUpPlanner
{
    /// <summary>
    /// Picks the unread chats to cover and writes the catch-up instructions.
    /// <paramref name="since"/> is an ISO date or time, or a span such as 30m, 2h, 1d or 1w.
    /// </summary>
    CatchUpPlan Plan(UnreadSummary summary, string? since);
}
