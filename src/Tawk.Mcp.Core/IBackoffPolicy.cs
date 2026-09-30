namespace Tawk.Mcp.Core;

public interface IBackoffPolicy
{
    /// <summary>The wait before retry number <paramref name="attempt"/>, counting from zero.</summary>
    TimeSpan NextDelay(int attempt);
}
