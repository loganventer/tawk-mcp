using Tawk.Mcp.Core;

namespace Tawk.Mcp.Managers.Memory;

public interface ITemplateManager
{
    Task<string> ListAsync(string? category, CancellationToken cancellationToken);

    Task<string> GetAsync(string name, CancellationToken cancellationToken);

    Task<string> SetAsync(string name, string body, string? description, string? category, string? voice, string? language, CancellationToken cancellationToken);

    Task<string> DeleteAsync(string name, IUserConfirmation? confirmation, CancellationToken cancellationToken);

    Task<string> RenderAsync(string name, string? chat, string? valuesJson, CancellationToken cancellationToken);

    /// <summary>Fills a template for a chat, refusing when any placeholder is left without a value.</summary>
    Task<PreparedDraft> PrepareDraftAsync(string name, string chat, string? valuesJson, CancellationToken cancellationToken);
}
