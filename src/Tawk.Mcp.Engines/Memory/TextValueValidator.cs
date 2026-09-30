using System.Text.Json;

namespace Tawk.Mcp.Engines.Memory;

public sealed class TextValueValidator : IFieldValueValidator
{
    private const int MaxLength = 2000;

    public bool Accepts(FieldKind kind) => kind == FieldKind.Text;

    public string? Problem(ContactFieldDefinition field, JsonElement value) =>
        value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 and <= MaxLength }
            ? null
            : $"must be text of 1 to {MaxLength} characters";
}
