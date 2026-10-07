using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Managers.Knowledge;

namespace Tawk.Mcp.Clients.Tools;

[McpServerToolType]
public sealed class KnowledgeTools(IKnowledgeManager knowledge)
{
    private const string Subject = "A chat's jid or name, self for the user, or a topic id such as concepts/cape-town-trip.";

    private const string Source =
        "Who says so: user (the user told you), contact (the person said it themselves), inferred (you worked it out from chats) or imported.";

    [McpServerTool(Name = "record_observation", Destructive = false, ReadOnly = false, Idempotent = false, OpenWorld = false)]
    [Description("Record something worth remembering about a person, the user or a topic, in a sentence or two. It keeps who said it and how sure "
        + "it is; an inferred one goes stale after about six months. Sensitive matters can only come from the user. Read what is stored first, with "
        + "list_observations or get_contact, and record only what is new: never the same thing twice." + ToolText.Memory)]
    public Task<CallToolResult> RecordObservationAsync(
        [Description("What it is about. " + Subject)] string about,
        [Description("The observation, in your own words. Never paste message text.")] string text,
        [Description(Source)] string source,
        [Description("One-word tags to find it by, such as [\"work\", \"travel\"].")] IReadOnlyList<string>? tags = null,
        [Description("How sure, 0 to 1. Defaults to 1 for stated things and 0.5 for inferred ones.")] double? confidence = null,
        [Description("True for health, beliefs, money and the like. Only with source user.")] bool sensitive = false,
        [Description("Where it came from, such as message ids. Never paste message text.")] string? evidence = null,
        [Description("Days until it should be checked again. Leave out for the default.")] int? staleAfterDays = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => knowledge.RecordObservationAsync(about, text, source, tags, confidence, sensitive, evidence, staleAfterDays, cancellationToken));

    [McpServerTool(Name = "list_observations", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List recorded observations, newest first, optionally about one person or topic, with a tag, or containing some text."
        + ToolText.Memory + ToolText.Stored)]
    public Task<CallToolResult> ListObservationsAsync(
        [Description("Only observations about this. " + Subject)] string? about = null,
        [Description("Only observations with this tag.")] string? tag = null,
        [Description("Only observations containing this text.")] string? query = null,
        [Description("Also show sensitive observations. Only when the user asks for them.")] bool includeSensitive = false,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => knowledge.ListObservationsAsync(about, tag, query, includeSensitive, cancellationToken));

    [McpServerTool(Name = "record_relation", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [Description("Record how two people or topics relate: the first is <label> the second, such as Anneke is \"spouse of\" self. "
        + "An inference never replaces what someone stated." + ToolText.Memory)]
    public Task<CallToolResult> RecordRelationAsync(
        [Description("The first. " + Subject)] string from,
        [Description("What the first is to the second, in a few words: \"spouse of\", \"works with\", \"organiser of\".")] string label,
        [Description("The second. " + Subject)] string to,
        [Description(Source)] string source,
        [Description("How sure, 0 to 1. Defaults to 1 for stated things and 0.5 for inferred ones.")] double? confidence = null,
        [Description("Anything worth adding, such as since when.")] string? note = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => knowledge.RecordRelationAsync(from, label, to, source, confidence, note, cancellationToken));

    [McpServerTool(Name = "get_knowledge", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Show what is known about a person, the user or a topic: how it relates to others, and the observations about it. "
        + "get_contact shows a person's profile fields." + ToolText.Memory + ToolText.Stored)]
    public Task<CallToolResult> GetKnowledgeAsync(
        [Description(Subject)] string subject,
        [Description("Also show sensitive observations. Only when the user asks for them.")] bool includeSensitive = false,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => knowledge.GetKnowledgeAsync(subject, includeSensitive, cancellationToken));

    [McpServerTool(Name = "forget_observation", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [Description("Remove one observation, by the id list_observations shows." + ToolText.Memory)]
    public Task<CallToolResult> ForgetObservationAsync(
        [Description("The observation's id, such as observations/27820000000@s.whatsapp.net-1790000000000.")] string id,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => knowledge.ForgetObservationAsync(id, cancellationToken));

    [McpServerTool(Name = "forget_relation", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [Description("Remove one relation between two people or topics." + ToolText.Memory)]
    public Task<CallToolResult> ForgetRelationAsync(
        [Description("The first. " + Subject)] string from,
        [Description("The relation's label, as get_knowledge shows it.")] string label,
        [Description("The second. " + Subject)] string to,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => knowledge.ForgetRelationAsync(from, label, to, cancellationToken));
}
