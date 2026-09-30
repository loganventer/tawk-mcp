namespace Tawk.Mcp.Core;

/// <summary>Raised by tawk-mcp itself whenever the state of its connection to tawk changes.</summary>
public sealed record ConnectionStateEvent(TawkConnectionState State, HelloInfo? Hello = null) : TawkEvent;
