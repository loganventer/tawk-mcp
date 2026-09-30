using Tawk.Mcp.Engines.Memory;

namespace Tawk.Mcp.Managers.Memory;

/// <summary>Picks which voice and which audience variant apply, from an explicit choice or from a contact's profile.</summary>
public interface IVoiceSelector
{
    Task<ResolvedVoice> SelectAsync(string? voice, string? audience, string? jid, CancellationToken cancellationToken);
}
