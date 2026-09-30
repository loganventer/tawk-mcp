using System.Text.Json;

namespace Tawk.Mcp.Core;

/// <summary>What happened to a write. Never carries a confirmation token.</summary>
public sealed record ConfirmationOutcome(ConfirmationStatus Status, JsonElement Result, string? Summary = null);
