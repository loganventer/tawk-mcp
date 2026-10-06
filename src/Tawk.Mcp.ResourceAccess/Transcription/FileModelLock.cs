namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>
/// A lock file in the model folder, held open for as long as a model is loaded. The system lets go of it
/// when its process ends, however it ends, so a crash never leaves it stuck.
/// </summary>
public sealed class FileModelLock(string path, TimeProvider clock) : IModelLock
{
    private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(250);

    public async Task<IDisposable?> AcquireAsync(TimeSpan wait, CancellationToken cancellationToken)
    {
        var until = clock.GetUtcNow() + wait;
        while (true)
        {
            if (TryOpen() is { } held)
            {
                return held;
            }

            if (clock.GetUtcNow() >= until)
            {
                return null;
            }

            await Task.Delay(Pause, clock, cancellationToken).ConfigureAwait(false);
        }
    }

    private FileStream? TryOpen()
    {
        try
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } folder)
            {
                Directory.CreateDirectory(folder);
            }

            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
