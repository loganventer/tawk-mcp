using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.ResourceAccess.Memory;

public interface IVoiceStore
{
    Task<IReadOnlyList<Voice>> ListAsync(CancellationToken cancellationToken);

    Task<Voice?> GetAsync(string name, CancellationToken cancellationToken);

    Task<Voice?> GetDefaultAsync(CancellationToken cancellationToken);

    /// <summary>Saves a voice. A default voice clears the default flag on every other voice.</summary>
    Task UpsertAsync(Voice voice, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(string name, CancellationToken cancellationToken);

    Task<IReadOnlyList<VoiceVariant>> ListVariantsAsync(string voice, CancellationToken cancellationToken);

    Task UpsertVariantAsync(VoiceVariant variant, CancellationToken cancellationToken);

    Task<bool> DeleteVariantAsync(string voice, string category, CancellationToken cancellationToken);
}
