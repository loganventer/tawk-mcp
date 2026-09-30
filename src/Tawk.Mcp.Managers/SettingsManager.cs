using System.Text.Json.Nodes;
using Tawk.Mcp.Core;
using Tawk.Mcp.ResourceAccess;

namespace Tawk.Mcp.Managers;

public sealed class SettingsManager(ITawkControl control, IConfirmationGate gate) : ISettingsManager
{
    public async Task<string> GetSettingsAsync(CancellationToken cancellationToken) =>
        WriteResults.Json(await control.RequestAsync("get_settings", null, cancellationToken).ConfigureAwait(false));

    public async Task<string> SetSettingAsync(string section, string key, string value, WriteContext context, CancellationToken cancellationToken) =>
        WriteResults.Describe(
            await gate.ExecuteAsync(
                "set_setting",
                new JsonObject { ["section"] = section, ["key"] = key, ["value"] = value },
                context.Confirmation,
                context.OnApprovalWaiting,
                cancellationToken).ConfigureAwait(false),
            r => $"{section}.{key} is now {WriteResults.String(r, "value") ?? value}.");

    public async Task<string> ListThemesAsync(CancellationToken cancellationToken) =>
        WriteResults.Json(await control.RequestAsync("list_themes", null, cancellationToken).ConfigureAwait(false));
}
