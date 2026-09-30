namespace Tawk.Mcp.Engines.Memory;

/// <summary>Whether a value may be stored in a field, and why not when it may not.</summary>
public sealed record FieldCheck(ContactFieldDefinition? Field, string? Problem)
{
    public bool Ok => Problem is null && Field is not null;
}
