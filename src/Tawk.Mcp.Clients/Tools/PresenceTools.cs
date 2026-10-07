using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Core;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Clients.Tools;

[McpServerToolType]
public sealed class PresenceTools(IPresenceManager presence, IAccountScope accounts)
{
    [McpServerTool(Name = "get_online_status", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Say whether the person in a one-to-one chat is online on WhatsApp now, or when they were last seen. One person per call. "
        + "It works only when the user switched on \"Look up online status\" in tawk (Settings, Automation), only for people who share "
        + "it with the user, and only while tawk itself shows as online. Use it when the user asks about someone, or to judge "
        + "whether now is a good moment for something the user asked you to do. Never call it over and over to keep watch on "
        + "someone, never write to someone because they are online, and never tell the other person you looked.")]
    public Task<CallToolResult> GetOnlineStatusAsync(
        [Description("The chat's jid or name. A person, not a group.")] string chat,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => presence.OnlineStatusAsync(chat, cancellationToken));
}
