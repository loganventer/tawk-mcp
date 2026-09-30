using System.Text.Json.Nodes;

namespace Tawk.Mcp.Tests.Fakes;

public sealed record RecordedRequest(string Op, JsonObject? Args);
