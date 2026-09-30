using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Clients.Tools;

[McpServerToolType]
public sealed class ProfileTools(IProfileManager profile)
{
    [McpServerTool(Name = "get_profile", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Show the user's own WhatsApp jid and name.")]
    public Task<CallToolResult> GetProfileAsync(CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => profile.GetProfileAsync(cancellationToken));

    [McpServerTool(Name = "set_profile", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = true)]
    [Description("Change the user's WhatsApp name, about text, or both." + ToolText.NeedsManage)]
    public Task<CallToolResult> SetProfileAsync(
        McpServer? server,
        [Description("The new name.")] string? name = null,
        [Description("The new about text.")] string? about = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => profile.SetProfileAsync(name, about, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "set_profile_photo", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = true)]
    [Description("Set the user's profile photo from a picture on this computer." + ToolText.NeedsManage)]
    public Task<CallToolResult> SetProfilePhotoAsync(
        McpServer? server,
        [Description("Path of the picture.")] string file,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => profile.SetProfilePhotoAsync(file, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "remove_profile_photo", Destructive = true, ReadOnly = false, Idempotent = true, OpenWorld = true)]
    [Description("Remove the user's profile photo." + ToolText.NeedsManage + ToolText.TwoStep)]
    public Task<CallToolResult> RemoveProfilePhotoAsync(
        McpServer? server,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => profile.RemoveProfilePhotoAsync(WriteContexts.For(server, progress), cancellationToken));
}
