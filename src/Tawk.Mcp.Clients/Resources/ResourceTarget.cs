using Tawk.Mcp.Clients.Sessions;

namespace Tawk.Mcp.Clients.Resources;

public sealed record ResourceTarget(IClientSession Session, string Uri);
