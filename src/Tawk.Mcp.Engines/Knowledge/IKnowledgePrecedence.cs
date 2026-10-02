using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Knowledge;

/// <summary>Decides which of two versions of the same thing is kept.</summary>
public interface IKnowledgePrecedence
{
    /// <summary>
    /// The stronger source wins: user, then contact, then imported, then inferred. Between equal sources
    /// the newer one wins, and an exact tie keeps what is already there.
    /// </summary>
    bool Replaces(FactSource incoming, DateTimeOffset incomingUpdated, FactSource current, DateTimeOffset currentUpdated);
}
