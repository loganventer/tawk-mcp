using Tawk.Mcp.Core.Okf;

namespace Tawk.Mcp.Engines.Knowledge;

/// <summary>An observation with the names of what it is about.</summary>
public sealed record ObservationView(OkfConcept Concept, IReadOnlyList<string> About);
