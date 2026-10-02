namespace Tawk.Mcp.Managers.Knowledge;

/// <summary>Exchanges knowledge with other Open Knowledge Format stores, as a folder of Markdown files.</summary>
public interface IOkfBundleManager
{
    Task<string> ExportAsync(string directory, bool includeSensitive, CancellationToken cancellationToken);

    Task<string> ImportAsync(string directory, CancellationToken cancellationToken);
}
