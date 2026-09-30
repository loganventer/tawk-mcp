using Microsoft.AspNetCore.Http;
using Tawk.Mcp.Core;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Host;

public static class HealthEndpoint
{
    public const string Path = "/healthz";

    public static IResult Handle(ILiveUpdatesManager liveUpdates)
    {
        ArgumentNullException.ThrowIfNull(liveUpdates);
        return Results.Json(new { tawk = TawkConnectionStates.ToWire(liveUpdates.ConnectionState) });
    }
}
