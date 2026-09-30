using System.Text.Json;

namespace Tawk.Mcp.Engines.Memory;

public sealed class BooleanValueValidator : IFieldValueValidator
{
    public bool Accepts(FieldKind kind) => kind == FieldKind.Boolean;

    public string? Problem(ContactFieldDefinition field, JsonElement value) =>
        value.ValueKind is JsonValueKind.True or JsonValueKind.False ? null : "must be true or false";
}
