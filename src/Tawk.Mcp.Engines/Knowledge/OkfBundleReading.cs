using Tawk.Mcp.Core.Okf;

namespace Tawk.Mcp.Engines.Knowledge;

/// <summary>What a bundle's files held, and what could not be read, one line per problem.</summary>
public sealed record OkfBundleReading(OkfBundle Bundle, IReadOnlyList<string> Problems);
