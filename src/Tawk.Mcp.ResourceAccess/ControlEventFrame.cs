using Tawk.Mcp.Core;

namespace Tawk.Mcp.ResourceAccess;

public sealed record ControlEventFrame(TawkEvent Event) : ControlFrame;
