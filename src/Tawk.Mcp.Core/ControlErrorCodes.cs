namespace Tawk.Mcp.Core;

public static class ControlErrorCodes
{
    private static readonly Dictionary<string, ControlErrorCode> ByWire = new(StringComparer.Ordinal)
    {
        ["bad_request"] = ControlErrorCode.BadRequest,
        ["hello_first"] = ControlErrorCode.HelloFirst,
        ["protocol"] = ControlErrorCode.Protocol,
        ["not_allowed"] = ControlErrorCode.NotAllowed,
        ["not_found"] = ControlErrorCode.NotFound,
        ["ambiguous"] = ControlErrorCode.Ambiguous,
        ["declined"] = ControlErrorCode.Declined,
        ["timed_out"] = ControlErrorCode.TimedOut,
        ["rate_limited"] = ControlErrorCode.RateLimited,
        ["offline"] = ControlErrorCode.Offline,
        ["failed"] = ControlErrorCode.Failed,
        ["draft_exists"] = ControlErrorCode.DraftExists,
        ["bad_token"] = ControlErrorCode.BadToken,
        ["unsupported"] = ControlErrorCode.Unsupported,
        ["not_running"] = ControlErrorCode.NotRunning,
    };

    public static ControlErrorCode Parse(string? code) =>
        code is not null && ByWire.TryGetValue(code, out var kind) ? kind : ControlErrorCode.Unknown;

    public static string ToWire(ControlErrorCode code)
    {
        foreach (var pair in ByWire)
        {
            if (pair.Value == code)
            {
                return pair.Key;
            }
        }

        return "unknown";
    }
}
