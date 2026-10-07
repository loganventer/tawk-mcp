using System.Text.Json.Nodes;
using Tawk.Mcp.Core;
using Tawk.Mcp.Engines;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Managers;

public sealed class PresenceManager(ITawkControl control, IPresenceFormatter formatter, IDelay delay, PresenceLookupOptions options)
    : IPresenceManager
{
    public async Task<string> OnlineStatusAsync(string chat, CancellationToken cancellationToken)
    {
        var presence = await AskAsync(chat, cancellationToken).ConfigureAwait(false);

        // The first answer is what tawk knew before it asked WhatsApp. While it is watching and knows nothing yet, the answer is on its way.
        for (var retry = 0; retry < options.Retries && presence is { State: ChatPresence.Unknown, Watching: true }; retry++)
        {
            await delay.DelayAsync(options.Wait, cancellationToken).ConfigureAwait(false);
            presence = await AskAsync(chat, cancellationToken).ConfigureAwait(false);
        }

        return formatter.Format(presence);
    }

    private Task<ChatPresence> AskAsync(string chat, CancellationToken cancellationToken) =>
        control.RequestAsync<ChatPresence>("presence", new JsonObject { ["chat"] = chat }, cancellationToken);
}
