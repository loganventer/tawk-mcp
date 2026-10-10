using System.Text.Json.Nodes;
using Tawk.Mcp.Core;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Managers;

public sealed class SummaryManager(ITawkControl control) : ISummaryManager
{
    public const string Kept = "tawk kept the summary.";

    public const string OlderTawk = "This tawk keeps no TL;DR summaries (it needs tawk 0.14.0 or later).";

    public async Task<string> KeepAsync(string messageId, string text, string? model, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(messageId);
        ArgumentException.ThrowIfNullOrEmpty(text);
        var hello = await control.ConnectAsync(cancellationToken).ConfigureAwait(false);
        if (!hello.Has(TawkFeatures.Summaries))
        {
            throw new TawkControlException(OlderTawk);
        }

        var args = new JsonObject { ["message_id"] = messageId, ["text"] = text, ["model"] = model ?? string.Empty };
        await control.RequestAsync("set_summary", args, cancellationToken).ConfigureAwait(false);
        return Kept;
    }
}
