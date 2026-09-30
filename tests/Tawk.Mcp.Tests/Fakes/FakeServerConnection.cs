using System.Net.Sockets;
using System.Text;

namespace Tawk.Mcp.Tests.Fakes;

public sealed class FakeServerConnection(Socket socket)
{
    private readonly NetworkStream _stream = new(socket, ownsSocket: true);
    private readonly SemaphoreSlim _write = new(1, 1);

    public StreamReader Reader { get; } = new(new NetworkStream(socket, ownsSocket: false), Encoding.UTF8);

    public async Task WriteAsync(string line)
    {
        await _write.WaitAsync();
        try
        {
            await _stream.WriteAsync(Encoding.UTF8.GetBytes(line + "\n"));
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
        }
        finally
        {
            _write.Release();
        }
    }

    public void Close()
    {
        try
        {
            socket.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException)
        {
        }

        _stream.Dispose();
    }
}
