using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

public interface IVoiceChecker
{
    VoiceReport Check(string draft, VoiceRules rules);
}
