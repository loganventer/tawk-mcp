using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Tawk.Mcp.Managers;

namespace Tawk.Mcp.Clients.Tools;

[McpServerToolType]
public sealed class SettingsTools(ISettingsManager settings)
{
    [McpServerTool(Name = "get_settings", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List tawk's settings with their values, choices and whether they may be changed from outside.")]
    public Task<CallToolResult> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => settings.GetSettingsAsync(cancellationToken));

    [McpServerTool(Name = "set_setting", Destructive = false, ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [Description("Change one tawk setting that get_settings marks as changeable. Automation settings, settings that run programs, "
        + "folders and files can never be changed this way." + ToolText.NeedsManage)]
    public Task<CallToolResult> SetSettingAsync(
        McpServer? server,
        [Description("The setting's section.")] string section,
        [Description("The setting's key.")] string key,
        [Description("The new value as text: on or off, a number, or one of its choices.")] string value,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => settings.SetSettingAsync(section, key, value, WriteContexts.For(server, progress), cancellationToken));

    [McpServerTool(Name = "list_themes", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List tawk's themes, for set_chat_theme.")]
    public Task<CallToolResult> ListThemesAsync(CancellationToken cancellationToken = default) =>
        ToolResults.RunAsync(() => settings.ListThemesAsync(cancellationToken));
}
