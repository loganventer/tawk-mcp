using System.Globalization;
using Tawk.Mcp.ResourceAccess.Transcription;

namespace Tawk.Mcp.Managers.Transcription;

public sealed class TranscriptionModelManager(IModelFiles files) : ITranscriptionModelManager
{
    public async Task<string> FetchAsync(string model, CancellationToken cancellationToken)
    {
        var name = (model ?? string.Empty).Trim().ToLowerInvariant();
        var path = await files.FetchAsync(name, cancellationToken).ConfigureAwait(false);
        var megabytes = new FileInfo(path).Length / (1024 * 1024);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Fetched the {name} model: {path} ({megabytes} MB).\nInstalled models: {string.Join(", ", files.Installed())}.");
    }
}
