using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Knowledge;

public sealed class KnowledgePrecedence : IKnowledgePrecedence
{
    public bool Replaces(FactSource incoming, DateTimeOffset incomingUpdated, FactSource current, DateTimeOffset currentUpdated) =>
        incoming != current ? incoming > current : incomingUpdated > currentUpdated;
}
