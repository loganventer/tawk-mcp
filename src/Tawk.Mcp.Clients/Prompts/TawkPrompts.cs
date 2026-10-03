using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Core;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Managers;
using Tawk.Mcp.Managers.Memory;

namespace Tawk.Mcp.Clients.Prompts;

[McpServerPromptType]
public sealed class TawkPrompts(IChatReadingManager reading, IDraftGuidance guidance, IAccountScope accounts)
{
    private const string AccountText = "Which of the user's WhatsApp accounts, by label or id. Leave out for the default account.";

    // The account is the user's own argument here, kept to one plain line before it is repeated to the model.
    private static string Named(string? account) =>
        string.IsNullOrWhiteSpace(account)
            ? string.Empty
            : $"\n\nThis is about the user's account {new string([.. account.Trim().Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_').Take(64)])}: "
              + "pass that account to every tool you call for it.";

    [McpServerPrompt(Name = "catch_up")]
    [Description("Catch up on unread WhatsApp messages in tawk.")]
    public async Task<string> CatchUpAsync(
        [Description("Only chats active since then: an ISO date or time, or a span such as 30m, 2h, 1d.")] string? since = null,
        [Description(AccountText)] string? account = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using (accounts.Use(account))
            {
                return await reading.CatchUpPromptAsync(since, cancellationToken).ConfigureAwait(false) + Named(account);
            }
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
        [Description(AccountText)] string? account = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using (accounts.Use(account))
            {
                var prompt = await reading.DraftReplyPromptAsync(chat, cancellationToken).ConfigureAwait(false) + Named(account);
                var voice = await guidance.ForChatAsync(chat, cancellationToken).ConfigureAwait(false);
                return voice.Length == 0 ? prompt : prompt + "\n\n" + voice;
            }
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
