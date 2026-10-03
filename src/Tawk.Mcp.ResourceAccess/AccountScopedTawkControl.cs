using System.Text.Json;
using System.Text.Json.Nodes;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.ResourceAccess;

/// <summary>
/// Adds the current account to every request. A tawk from before accounts would ignore the name and act
/// on its one account, so a request that names an account is refused there instead of being sent.
/// </summary>
public sealed class AccountScopedTawkControl(ITawkControl inner, IAccountScope scope) : ITawkControl
{
    public const string OldTawk =
        "This tawk has one account and cannot be asked for another by name. Update tawk, or leave the account out.";

    public TawkConnectionState State => inner.State;

    public IAsyncEnumerable<TawkEvent> Events => inner.Events;

    public Task<HelloInfo> ConnectAsync(CancellationToken cancellationToken) => inner.ConnectAsync(cancellationToken);

    public async Task<JsonElement> RequestAsync(
        string op, JsonObject? args, Action? onApprovalWaiting, CancellationToken cancellationToken)
    {
        var account = scope.Current;
        if (account is null || args?.ContainsKey("account") == true)
        {
            return await inner.RequestAsync(op, args, onApprovalWaiting, cancellationToken).ConfigureAwait(false);
        }

        var hello = await inner.ConnectAsync(cancellationToken).ConfigureAwait(false);
        if (!hello.MultiAccount)
        {
            throw new TawkControlException(ControlErrorCode.Unsupported, OldTawk);
        }

        var named = args is null ? new JsonObject() : (JsonObject)args.DeepClone();
        named["account"] = account;
        return await inner.RequestAsync(op, named, onApprovalWaiting, cancellationToken).ConfigureAwait(false);
    }
}
