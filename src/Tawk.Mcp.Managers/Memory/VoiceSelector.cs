using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Engines.Memory;
using Tawk.Mcp.ResourceAccess.Memory;

namespace Tawk.Mcp.Managers.Memory;

public sealed class VoiceSelector(IVoiceStore voices, IContactStore contacts, IVoiceResolver resolver) : IVoiceSelector
{
    public async Task<ResolvedVoice> SelectAsync(string? voice, string? audience, string? jid, CancellationToken cancellationToken)
    {
        var contact = jid is null ? null : await contacts.GetAsync(jid, cancellationToken).ConfigureAwait(false);
        var chosen = await PickAsync(voice ?? contact?.Voice, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<string> categories = audience is not null
            ? [CategoryPaths.Normalise(audience)]
            : jid is null ? [] : await contacts.GetCategoriesAsync(jid, cancellationToken).ConfigureAwait(false);
        var variants = await voices.ListVariantsAsync(chosen.Name, cancellationToken).ConfigureAwait(false);
        return resolver.Resolve(chosen, variants, categories);
    }

    private async Task<Voice> PickAsync(string? name, CancellationToken cancellationToken)
    {
        if (name is not null)
        {
            return await voices.GetAsync(name, cancellationToken).ConfigureAwait(false)
                ?? throw new MemoryException($"There is no voice called {name}. list_voices shows them.");
        }

        if (await voices.GetDefaultAsync(cancellationToken).ConfigureAwait(false) is { } fallback)
        {
            return fallback;
        }

        var all = await voices.ListAsync(cancellationToken).ConfigureAwait(false);
        return all.Count switch
        {
            0 => throw new MemoryException("There are no voices yet. Add one with set_voice or import_voice."),
            1 => all[0],
            _ => throw new MemoryException("Several voices exist and none is the default. Name one, or mark one as the default with set_voice."),
        };
    }
}
