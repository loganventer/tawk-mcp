using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Managers.Memory;

namespace Tawk.Mcp.Clients.Tools;

[McpServerToolType]
public sealed class VoiceTools(IVoiceManager voices)
{
    private const string Rules =
        "Checkable rules as JSON, every key optional: languages ([\"af\",\"en\",\"mix\"]), case (lower|sentence|any), max_words, max_emoji, "
        + "allowed_emoji, required_address_forms, forbidden_address_forms, must_include_any, forbidden_patterns (regular expressions), "
        + "greeting and signoff (none|optional|required).";

    [McpServerTool(Name = "list_voices", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List the writing voices tawk-mcp knows." + ToolText.Memory)]
    public Task<CallToolResult> ListVoicesAsync(CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => voices.ListVoicesAsync(cancellationToken));

    [McpServerTool(Name = "get_voice", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Show how to write: for a chat (using the contact's categories), for an audience category, or a whole voice. "
        + "Read it before drafting a message." + ToolText.Memory + ToolText.Stored)]
    public Task<CallToolResult> GetVoiceAsync(
        [Description("A voice name. Leave it out for the contact's voice or the default.")] string? voice = null,
        [Description("An audience category, such as friends/close.")] string? audience = null,
        [Description("A chat's jid or name, to use that contact's categories.")] string? chat = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => voices.GetVoiceAsync(voice, audience, chat, cancellationToken));

    [McpServerTool(Name = "set_voice", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [Description("Create a voice or change its guide, rules, description or default flag. The first voice becomes the default." + ToolText.Memory)]
    public Task<CallToolResult> SetVoiceAsync(
        [Description("The voice's name, such as logan.")] string name,
        [Description("Markdown describing how the user writes everywhere. Needed for a new voice.")] string? guide = null,
        [Description(Rules)] string? rules = null,
        [Description("A short description.")] string? description = null,
        [Description("Make this the voice used when none is named.")] bool? isDefault = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => voices.SetVoiceAsync(name, description, guide, rules, isDefault, cancellationToken));

    [McpServerTool(Name = "set_voice_variant", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [Description("Set how a voice changes for one audience category. Its rules override the voice's rules one by one." + ToolText.Memory)]
    public Task<CallToolResult> SetVoiceVariantAsync(
        [Description("The voice's name.")] string voice,
        [Description("The audience category, created if new.")] string category,
        [Description("Markdown describing how the user writes to this audience. Needed for a new variant.")] string? guide = null,
        [Description(Rules)] string? rules = null,
        [Description("Short verbatim examples of how the user writes to this audience.")] IReadOnlyList<string>? examples = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => voices.SetVariantAsync(voice, category, guide, rules, examples, cancellationToken));

    [McpServerTool(Name = "import_voice", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [Description("Import a Markdown voice guide. Sections whose headings are mapped to categories become audience variants; the rest is the base guide."
        + ToolText.Memory)]
    public Task<CallToolResult> ImportVoiceAsync(
        [Description("The voice's name.")] string name,
        [Description("The whole Markdown guide.")] string markdown,
        [Description("Heading text to category, such as {\"Wife (Anneke)\": \"family/spouse\"}.")] Dictionary<string, string>? sections = null,
        [Description("A short description.")] string? description = null,
        [Description("Make this the default voice.")] bool isDefault = false,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => voices.ImportVoiceAsync(name, markdown, sections, description, isDefault, cancellationToken));

    [McpServerTool(Name = "export_voice", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Export a voice and its variants as one Markdown guide." + ToolText.Memory + ToolText.Stored)]
    public Task<CallToolResult> ExportVoiceAsync(
        [Description("The voice's name.")] string name,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => voices.ExportVoiceAsync(name, cancellationToken));

    [McpServerTool(Name = "delete_voice", Destructive = true, ReadOnly = false, Idempotent = false, OpenWorld = false)]
    [Description("Delete a voice, or only one of its audience variants." + ToolText.Memory + ToolText.DeletesMemory)]
    public Task<CallToolResult> DeleteVoiceAsync(
        McpServer? server,
        [Description("The voice's name.")] string name,
        [Description("Only delete the variant for this category.")] string? category = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => voices.DeleteVoiceAsync(name, category, ElicitationConfirmation.ForMemory(server), cancellationToken));

    [McpServerTool(Name = "check_voice", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Score a draft out of 100 against the user's voice for a chat or audience, list what the rules flag, and show the guide "
        + "to judge the tone by. Use it before showing or drafting a message, and fix what it finds." + ToolText.Memory + ToolText.Stored)]
    public Task<CallToolResult> CheckVoiceAsync(
        [Description("The draft message.")] string draft,
        [Description("The chat it is for (jid or name), to use that contact's categories.")] string? chat = null,
        [Description("An audience category, instead of a chat.")] string? audience = null,
        [Description("A voice name. Leave it out for the contact's voice or the default.")] string? voice = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => voices.CheckVoiceAsync(draft, chat, audience, voice, cancellationToken));

    [McpServerTool(Name = "learn_voice", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [Description("Measure the user's own recent messages in some chats and store the averages (length, case, emoji) on a voice's audience variant, "
        + "so check_voice can compare drafts with how the user really writes. Only the numbers are stored, never the messages." + ToolText.Memory)]
    public Task<CallToolResult> LearnVoiceAsync(
        [Description("The voice's name.")] string voice,
        [Description("The audience category these chats belong to.")] string category,
        [Description("Chats (jids or names) where the user writes to this audience.")] IReadOnlyList<string> chats,
        [Description("How many recent messages to read in each chat, 1 to 200. Default 100.")] int messagesPerChat = 100,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => voices.LearnVoiceAsync(voice, category, chats, messagesPerChat, cancellationToken));
}
