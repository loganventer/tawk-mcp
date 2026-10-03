using Tawk.Mcp.Core;

namespace Tawk.Mcp.Clients;

/// <summary>The name each kind of message activity goes by on the channel and the event stream.</summary>
public static class ActivityNames
{
    public static string Of(ActivityKind kind) => kind switch
    {
        ActivityKind.Reaction => "reaction",
        ActivityKind.Edited => "edit",
        ActivityKind.Deleted => "delete",
        _ => "scheduled_sent",
    };
}
