namespace Tawk.Mcp.Core.Media;

/// <summary>A media request that cannot be done as asked. The message is written for the model and the user.</summary>
public sealed class MediaException : Exception
{
    public MediaException()
        : this("The media request failed.")
    {
    }

    public MediaException(string message)
        : base(message)
    {
    }

    public MediaException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
