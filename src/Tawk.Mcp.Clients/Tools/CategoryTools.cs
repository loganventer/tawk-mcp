using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Managers.Memory;

namespace Tawk.Mcp.Clients.Tools;

[McpServerToolType]
public sealed class CategoryTools(ICategoryManager categories)
{
    [McpServerTool(Name = "list_categories", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List the audience categories voices, contacts and templates are grouped by, such as family/spouse or work/peers." + ToolText.Memory)]
    public Task<CallToolResult> ListCategoriesAsync(CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => categories.ListAsync(cancellationToken));

    [McpServerTool(Name = "set_category", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [Description("Add an audience category or change its description. Paths nest with slashes; a voice variant for family also covers family/spouse." + ToolText.Memory)]
    public Task<CallToolResult> SetCategoryAsync(
        [Description("The path, such as family/spouse, friends/close, work/formal, services or elders.")] string path,
        [Description("Who belongs in it.")] string? description = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => categories.SetAsync(path, description, cancellationToken));

    [McpServerTool(Name = "delete_category", Destructive = true, ReadOnly = false, Idempotent = false, OpenWorld = false)]
    [Description("Delete a category, with its voice variants and its use on contacts and templates." + ToolText.Memory + ToolText.DeletesMemory)]
    public Task<CallToolResult> DeleteCategoryAsync(
        McpServer? server,
        [Description("The category path.")] string path,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => categories.DeleteAsync(path, ElicitationConfirmation.ForMemory(server), cancellationToken));
}
