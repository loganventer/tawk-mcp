using Tawk.Mcp.Core;

namespace Tawk.Mcp.Managers;

public interface ISettingsManager
{
    Task<string> GetSettingsAsync(CancellationToken cancellationToken);

    Task<string> SetSettingAsync(string section, string key, string value, WriteContext context, CancellationToken cancellationToken);

    Task<string> ListThemesAsync(CancellationToken cancellationToken);
}
