using Tawk.Mcp.Core.Transcription;
using Whisper.net.Ggml;

namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>Models are GGML files named ggml-NAME.bin in one private folder.</summary>
public sealed class DiskModelFiles(TranscriptionOptions options) : IModelFiles
{
    public string Folder => options.ModelDirectory;

    /// <summary>~/.local/share/tawk-mcp/models, or under $XDG_DATA_HOME when that is set.</summary>
    public static string DefaultFolder(Func<string, string?> environment, string home)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var data = environment("XDG_DATA_HOME") is { Length: > 0 } set ? set : Path.Combine(home, ".local", "share");
        return Path.Combine(data, "tawk-mcp", "models");
    }

    public string? Find(string model)
    {
        if (!Known(model))
        {
            return null;
        }

        var path = PathOf(model);
        return File.Exists(path) ? path : null;
    }

    public IReadOnlyList<string> Installed() => [.. TranscriptionOptions.KnownModels.Where(m => File.Exists(PathOf(m)))];

    public async Task<string> FetchAsync(string model, CancellationToken cancellationToken)
    {
        if (!Known(model))
        {
            throw new TranscriptionException($"There is no model called {model}. The models are: {string.Join(", ", TranscriptionOptions.KnownModels)}.");
        }

        var type = model switch
        {
            "tiny" => GgmlType.Tiny,
            "base" => GgmlType.Base,
            "small" => GgmlType.Small,
            "medium" => GgmlType.Medium,
            "large-v3-turbo" => GgmlType.LargeV3Turbo,
            _ => GgmlType.LargeV3,
        };
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(Folder);
        }
        else
        {
            Directory.CreateDirectory(Folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var path = PathOf(model);
        var partial = path + ".part";
        try
        {
            var download = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(type, QuantizationType.NoQuantization, cancellationToken).ConfigureAwait(false);
            await using (download.ConfigureAwait(false))
            {
                var file = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None);
                await using (file.ConfigureAwait(false))
                {
                    await download.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
                }
            }

            // Only a whole file takes the model's name, so a download that was cut short is never loaded.
            File.Move(partial, path, overwrite: true);
            return path;
        }
        catch (OperationCanceledException)
        {
            File.Delete(partial);
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            File.Delete(partial);
            throw new TranscriptionException($"The model could not be fetched: {ex.Message}", ex);
        }
    }

    private static bool Known(string model) => TranscriptionOptions.KnownModels.Contains(model, StringComparer.Ordinal);

    private string PathOf(string model) => Path.Combine(Folder, $"ggml-{model}.bin");
}
