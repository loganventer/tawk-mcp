using System.Text.Json;
using Tawk.Mcp.Core;

namespace Tawk.Mcp.ResourceAccess;

public sealed record ControlResponse(string Id, bool Ok, JsonElement Result, ControlError? Error) : ControlFrame;
