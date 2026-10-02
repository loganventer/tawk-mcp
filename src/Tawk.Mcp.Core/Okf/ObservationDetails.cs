using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Core.Okf;

/// <summary>What memory records about a concept beyond OKF's own keys. Kept as producer keys in the frontmatter.</summary>
public sealed record ObservationDetails(FactSource Source, double Confidence, bool Sensitive, string? Evidence);
