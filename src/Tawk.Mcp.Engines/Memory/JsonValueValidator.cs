using System.Text.Json;

namespace Tawk.Mcp.Engines.Memory;

public sealed class JsonValueValidator : IFieldValueValidator
{
    private const int MaxLength = 4000;

    public bool Accepts(FieldKind kind) => kind == FieldKind.Json;

    public string? Problem(ContactFieldDefinition field, JsonElement value) =>
        value.ValueKind is JsonValueKind.Object or JsonValueKind.Array && value.GetRawText().Length <= MaxLength
            ? null
            : $"must be a JSON object or list of up to {MaxLength} characters";
}
