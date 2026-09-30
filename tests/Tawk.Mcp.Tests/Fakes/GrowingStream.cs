using System.Text;

namespace Tawk.Mcp.Tests.Fakes;

/// <summary>A response body that can be read safely while the handler is still writing to it.</summary>
public sealed class GrowingStream : Stream
{
    private readonly StringBuilder _text = new();

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public string Text
    {
        get
        {
            lock (_text)
            {
                return _text.ToString();
            }
        }
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        lock (_text)
        {
            _text.Append(Encoding.UTF8.GetString(buffer, offset, count));
        }
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        lock (_text)
        {
            _text.Append(Encoding.UTF8.GetString(buffer.Span));
        }

        return ValueTask.CompletedTask;
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
