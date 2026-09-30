using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Managers;
using Tawk.Mcp.Managers.Memory;

namespace Tawk.Mcp.Clients.Prompts;

[McpServerPromptType]
public sealed class TawkPrompts(IChatReadingManager reading, IDraftGuidance guidance)
{
    [McpServerPrompt(Name = "catch_up")]
    [Description("Catch up on unread WhatsApp messages in tawk.")]
    public async Task<string> CatchUpAsync(
        [Description("Only chats active since then: an ISO date or time, or a span such as 30m, 2h, 1d.")] string? since = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await reading.CatchUpPromptAsync(since, cancellationToken).ConfigureAwait(false);
        }
        catch (TawkControlException ex)
        {
            throw new McpException(ControlErrorMessages.Describe(ex), ex);
        }
    }

    [McpServerPrompt(Name = "draft_reply")]
    [Description("Draft a reply for a chat and show it without sending.")]
    public async Task<string> DraftReplyAsync(
        [Description("The chat's jid or name.")] string chat,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = await reading.DraftReplyPromptAsync(chat, cancellationToken).ConfigureAwait(false);
            var voice = await guidance.ForChatAsync(chat, cancellationToken).ConfigureAwait(false);
            return voice.Length == 0 ? prompt : prompt + "\n\n" + voice;
        }
        catch (TawkControlException ex)
        {
            throw new McpException(ControlErrorMessages.Describe(ex), ex);
        }
        catch (MemoryException ex)
        {
            throw new McpException(ex.Message, ex);
        }
    }
}
