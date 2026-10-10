using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Core;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Clients.Tools;

[McpServerToolType]
public sealed class SummaryTools(ISummaryManager summaries, IAccountScope accounts)
{
    [McpServerTool(Name = "set_summary", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [Description(
        "Hand tawk the TL;DR of a message, which tawk then shows in place of the text until the user unfolds it. Use it only to answer "
        + "a channel event of type summary_wanted, with the message_id from that event: tawk asks for the chats the user put in TL;DR mode, "
        + "and refuses a summary for any other chat. Write one plain paragraph within the max_chars the event gives, in the message's own "
        + "language, saying only what the message says, and shorter than the message: tawk shows a summary only when it is shorter, so a "
        + "few words do for a short message. Nothing is sent to WhatsApp and the user is not asked.")]
    public Task<CallToolResult> SetSummaryAsync(
        [Description("The id of the message, from the summary_wanted event.")] string messageId,
        [Description("The summary: one plain paragraph, no formatting.")] string text,
        [Description("The name of the model writing it, when you know it.")] string? model = null,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => summaries.KeepAsync(messageId, text, model, cancellationToken));
}
