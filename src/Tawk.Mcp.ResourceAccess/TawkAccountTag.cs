using System.Globalization;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.ResourceAccess;

/// <summary>Finds the current account's jid in what tawk said when it greeted this instance.</summary>
public sealed class TawkAccountTag(ITawkControl control, IAccountScope scope) : IAccountTag
{
    public async Task<string?> CurrentAsync(CancellationToken cancellationToken)
    {
        HelloInfo hello;
        try
        {
            hello = await control.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (TawkControlException)
        {
            return null;                                    // memory works without tawk; the tag is then left out
        }

        var accounts = hello.Accounts ?? [];
        var named = scope.Current;
        var match = named is null
            ? accounts.FirstOrDefault(a => a.Id == hello.DefaultAccount)
            : accounts.FirstOrDefault(a =>
                string.Equals(a.Label, named, StringComparison.OrdinalIgnoreCase)
                || a.Id.ToString(CultureInfo.InvariantCulture) == named);
        var jid = match?.Jid ?? (named is null ? hello.Account?.Jid : null);
        return string.IsNullOrEmpty(jid) ? null : jid;
    }
}
