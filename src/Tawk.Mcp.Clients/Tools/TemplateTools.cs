using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Core;
using Tawk.Mcp.Managers;
using Tawk.Mcp.Managers.Memory;

namespace Tawk.Mcp.Clients.Tools;

[McpServerToolType]
public sealed class TemplateTools(ITemplateManager templates, IMessageSendingManager sending, IAccountScope accounts)
{
    private const string Values = "Values for placeholders as a JSON object, such as {\"day\": \"Saturday\"}. They override values from the contact.";

    [McpServerTool(Name = "list_templates", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List reply templates, optionally for one audience category." + ToolText.Memory + ToolText.Stored)]
    public Task<CallToolResult> ListTemplatesAsync(
        [Description("Only templates in this category or below it.")] string? category = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => templates.ListAsync(category, cancellationToken));

    [McpServerTool(Name = "get_template", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Show a template and its placeholders." + ToolText.Memory + ToolText.Stored)]
    public Task<CallToolResult> GetTemplateAsync(
        [Description("The template's name.")] string name,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => templates.GetAsync(name, cancellationToken));

    [McpServerTool(Name = "set_template", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [Description("Create or replace a reply template. Placeholders look like {{day}}; {{contact.name}}, {{contact.first_name}} and "
        + "{{contact.<field>}} fill from the contact's profile." + ToolText.Memory)]
    public Task<CallToolResult> SetTemplateAsync(
        [Description("The template's name, such as birthday or running-late.")] string name,
        [Description("The text, in the user's voice, with {{placeholders}}.")] string body,
        [Description("When to use it.")] string? description = null,
        [Description("The audience category it is written for.")] string? category = null,
        [Description("The voice it is written in.")] string? voice = null,
        [Description("Its language, such as af or en.")] string? language = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => templates.SetAsync(name, body, description, category, voice, language, cancellationToken));

    [McpServerTool(Name = "delete_template", Destructive = true, ReadOnly = false, Idempotent = false, OpenWorld = false)]
    [Description("Delete a reply template." + ToolText.Memory + ToolText.DeletesMemory)]
    public Task<CallToolResult> DeleteTemplateAsync(
        McpServer? server,
        [Description("The template's name.")] string name,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => templates.DeleteAsync(name, ElicitationConfirmation.ForMemory(server), cancellationToken));

    [McpServerTool(Name = "render_template", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Fill a template, optionally for a chat, and check it against the voice. Nothing is sent or drafted." + ToolText.Memory + ToolText.Stored)]
    public Task<CallToolResult> RenderTemplateAsync(
        [Description("The template's name.")] string name,
        [Description("The chat it is for (jid or name), to fill {{contact.*}}.")] string? chat = null,
        [Description(Values)] string? values = null,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => templates.RenderAsync(name, chat, values, cancellationToken));

    [McpServerTool(Name = "draft_template", Destructive = false, ReadOnly = false, Idempotent = false, OpenWorld = true)]
    [Description("Fill a template for a chat and put it into that chat's draft in tawk for the user to edit and send. Nothing is sent. "
        + "Refuses if a placeholder has no value. Needs access = send in tawk, and fails if the chat already has a draft.")]
    public Task<CallToolResult> DraftTemplateAsync(
        [Description("The template's name.")] string name,
        [Description("The chat's jid or name.")] string chat,
        [Description(Values)] string? values = null,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, async () =>
        {
            var draft = await templates.PrepareDraftAsync(name, chat, values, cancellationToken).ConfigureAwait(false);
            return await sending.DraftMessageAsync(draft.Jid, draft.Text, cancellationToken).ConfigureAwait(false) + draft.Notes;
        });
}
