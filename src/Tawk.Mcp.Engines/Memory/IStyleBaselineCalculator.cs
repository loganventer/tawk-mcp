using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

public interface IStyleBaselineCalculator
{
    /// <summary>Averages the style of the given messages. Only numbers are kept; the text is not.</summary>
    StyleBaseline Calculate(IReadOnlyList<string> messages);
}
