using Tawk.Mcp.ResourceAccess.Sync;

namespace Tawk.Mcp.Tests.Fakes;

/// <summary>A remote memory file held in memory, shared by the machines of a test.</summary>
public sealed class FakeMemoryRemote : IMemoryRemote
{
    private byte[]? _content;
    private int _version;

    public int Pushes { get; private set; }

    public int Downloads { get; private set; }

    /// <summary>How many pushes to refuse as if another machine had got in first.</summary>
    public int ConflictsToCome { get; set; }

    public List<string> Messages { get; } = [];

    public Task<string?> HeadAsync(CancellationToken cancellationToken) => Task.FromResult(_content is null ? null : Version);

    public async Task DownloadAsync(string version, string destination, CancellationToken cancellationToken)
    {
        Downloads++;
        await File.WriteAllBytesAsync(destination, _content!, cancellationToken);
    }

    public async Task<string?> PushAsync(string file, string? baseVersion, string message, CancellationToken cancellationToken)
    {
        if (ConflictsToCome > 0)
        {
            ConflictsToCome--;
            return null;
        }

        if (baseVersion != (_content is null ? null : Version))
        {
            return null;
        }

        _content = await File.ReadAllBytesAsync(file, cancellationToken);
        _version++;
        Pushes++;
        Messages.Add(message);
        return Version;
    }

    /// <summary>Puts a file there as if some other machine had pushed it.</summary>
    public void Put(byte[] content)
    {
        _content = content;
        _version++;
    }

    private string Version => "v" + _version.ToString("0000000", System.Globalization.CultureInfo.InvariantCulture);
}
