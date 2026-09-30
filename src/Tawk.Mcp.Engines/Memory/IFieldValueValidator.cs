using System.Text.Json;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>Checks values of one kind. Returns null when the value is fine, or a short reason when it is not.</summary>
public interface IFieldValueValidator
{
    bool Accepts(FieldKind kind);

    string? Problem(ContactFieldDefinition field, JsonElement value);
}
