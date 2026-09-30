namespace Tawk.Mcp.Core.Memory;

/// <summary>A memory request that cannot be done as asked. The message is written for the model and the user.</summary>
public sealed class MemoryException : Exception
{
    public MemoryException()
        : this("The memory request failed.")
    {
    }

    public MemoryException(string message)
        : base(message)
    {
    }

    public MemoryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
