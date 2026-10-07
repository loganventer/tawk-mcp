using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Core;
using Tawk.Mcp.Managers.Memory;

namespace Tawk.Mcp.Clients.Tools;

[McpServerToolType]
public sealed class ContactTools(IContactProfileManager profiles, IAccountScope accounts)
{
    private const string Source =
        "Who says so: user (the user told you), contact (the person said it themselves), inferred (you worked it out from chats) or imported.";

    [McpServerTool(Name = "list_contact_fields", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List the fields a contact profile can hold, their values, and which may never be inferred." + ToolText.Memory)]
    public Task<CallToolResult> ListContactFieldsAsync() =>
        ToolResults.RunAsync(() => Task.FromResult(profiles.ListFields()));

    [McpServerTool(Name = "get_contact", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Show what tawk-mcp remembers about a contact: categories, voice, fields with their source and confidence, and notes. "
        + "Read it before writing to someone." + ToolText.Memory + ToolText.Stored)]
    public Task<CallToolResult> GetContactAsync(
        [Description("The chat's jid or name.")] string chat,
        [Description("Also show sensitive fields. Only when the user asks for them.")] bool includeSensitive = false,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => profiles.GetContactAsync(chat, includeSensitive, cancellationToken));

    [McpServerTool(Name = "list_contacts", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List contacts tawk-mcp has profiles for, optionally in a category or matching a name." + ToolText.Memory + ToolText.Stored)]
    public Task<CallToolResult> ListContactsAsync(
        [Description("Only contacts in this category or below it.")] string? category = null,
        [Description("Only contacts whose name or jid contains this.")] string? query = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => profiles.ListContactsAsync(category, query, cancellationToken));

    [McpServerTool(Name = "set_contact_fields", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [Description("Store profile fields for a contact. Each keeps its source and confidence; inferred values lapse after a while, and an inference "
        + "never replaces something the user or contact stated. Sensitive matters can only come from the user." + ToolText.Memory)]
    public Task<CallToolResult> SetContactFieldsAsync(
        [Description("The chat's jid or name.")] string chat,
        [Description("Fields as a JSON object, such as {\"relation\": \"friend\", \"address_form\": \"jy\"}. list_contact_fields shows them.")] string fields,
        [Description(Source)] string source,
        [Description("How sure, 0 to 1. Defaults to 1 for stated values and 0.5 for inferred ones.")] double? confidence = null,
        [Description("Where it came from, such as message ids. Never paste message text.")] string? evidence = null,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => profiles.SetFieldsAsync(chat, fields, source, confidence, evidence, cancellationToken));

    [McpServerTool(Name = "add_contact_note", Destructive = false, ReadOnly = false, Idempotent = false, OpenWorld = false)]
    [Description("Add a free-text note to a contact's profile, for anything no field covers. Read the profile first with get_contact, and "
        + "do not add what a field, a note or an observation already says: something recorded with record_observation needs no note as well." + ToolText.Memory)]
    public Task<CallToolResult> AddContactNoteAsync(
        [Description("The chat's jid or name.")] string chat,
        [Description("The note, in a sentence or two.")] string text,
        [Description(Source)] string source,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => profiles.AddNoteAsync(chat, text, source, cancellationToken));

    [McpServerTool(Name = "forget_contact_field", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [Description("Remove one field from a contact's profile." + ToolText.Memory)]
    public Task<CallToolResult> ForgetContactFieldAsync(
        [Description("The chat's jid or name.")] string chat,
        [Description("The field's name.")] string field,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => profiles.ForgetFieldAsync(chat, field, cancellationToken));

    [McpServerTool(Name = "set_contact_categories", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [Description("Put a contact in audience categories (replacing the old ones), which decides the voice variant used for them. "
        + "Optionally tie them to a named voice." + ToolText.Memory)]
    public Task<CallToolResult> SetContactCategoriesAsync(
        [Description("The chat's jid or name.")] string chat,
        [Description("Category paths in order of preference, such as [\"family/spouse\"]. New ones are created.")] IReadOnlyList<string> categories,
        [Description("A voice for this contact, or an empty string for the default.")] string? voice = null,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => profiles.SetCategoriesAsync(chat, categories, voice, cancellationToken));

    [McpServerTool(Name = "delete_contact", Destructive = true, ReadOnly = false, Idempotent = false, OpenWorld = false)]
    [Description("Forget everything tawk-mcp has stored about a contact." + ToolText.Memory + ToolText.DeletesMemory)]
    public Task<CallToolResult> DeleteContactAsync(
        McpServer? server,
        [Description("The chat's jid or name.")] string chat,
        [Description(ToolText.Account)] string? account = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(accounts, account, () => profiles.DeleteContactAsync(chat, ElicitationConfirmation.ForMemory(server), cancellationToken));

    [McpServerTool(Name = "due_follow_ups", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List the follow-ups stored on contacts that are due, such as asking how an interview went." + ToolText.Memory + ToolText.Stored)]
    public Task<CallToolResult> DueFollowUpsAsync(
        [Description("Include those due within this many days. Default 0, due today or earlier.")] int withinDays = 0,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => profiles.DueFollowUpsAsync(withinDays, cancellationToken));
}
