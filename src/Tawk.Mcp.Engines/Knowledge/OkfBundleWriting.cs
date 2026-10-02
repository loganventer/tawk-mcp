using Tawk.Mcp.Core.Okf;

namespace Tawk.Mcp.Engines.Knowledge;

/// <summary>A bundle as files, and the ids of concepts left out because their id cannot be a file path.</summary>
public sealed record OkfBundleWriting(IReadOnlyList<OkfBundleFile> Files, IReadOnlyList<string> Skipped);
