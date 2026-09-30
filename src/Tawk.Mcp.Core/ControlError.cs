namespace Tawk.Mcp.Core;

public sealed record ControlError(
    string Code,
    string Message,
    IReadOnlyList<ChatRef>? Candidates = null,
    int? RetryAfter = null)
{
    public ControlErrorCode Kind => ControlErrorCodes.Parse(Code);
}
