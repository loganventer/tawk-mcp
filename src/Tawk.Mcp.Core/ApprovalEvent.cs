namespace Tawk.Mcp.Core;

public sealed record ApprovalEvent(string RequestId, string State) : TawkEvent;
