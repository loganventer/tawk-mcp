using System.Text.Json;
using System.Text.Json.Nodes;

namespace Tawk.Mcp.ResourceAccess;

public static class TawkControlExtensions
{
    public static Task<JsonElement> RequestAsync(
        this ITawkControl control,
        string op,
        JsonObject? args,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.RequestAsync(op, args, null, cancellationToken);
    }

    public static async Task<T> RequestAsync<T>(
        this ITawkControl control,
        string op,
        JsonObject? args,
        Action? onApprovalWaiting,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(control);
        var result = await control.RequestAsync(op, args, onApprovalWaiting, cancellationToken).ConfigureAwait(false);
        return ControlLineCodec.Deserialize<T>(result);
    }

    public static Task<T> RequestAsync<T>(
        this ITawkControl control,
        string op,
        JsonObject? args,
        CancellationToken cancellationToken) =>
        control.RequestAsync<T>(op, args, null, cancellationToken);
}
