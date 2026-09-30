namespace Tawk.Mcp.Core;

public static class TawkConnectionStates
{
    public static string ToWire(TawkConnectionState state) => state switch
    {
        TawkConnectionState.Connected => "connected",
        TawkConnectionState.CircuitOpen => "circuit_open",
        _ => "waiting",
    };
}
