using Tawk.Mcp.Core;

namespace Tawk.Mcp.Engines;

public interface IDraftReplyPlanner
{
    string Instructions(ChatSummary chat);
}
