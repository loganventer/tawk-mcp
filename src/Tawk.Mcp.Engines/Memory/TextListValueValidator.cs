using System.Text.Json;

namespace Tawk.Mcp.Engines.Memory;

public sealed class TextListValueValidator : IFieldValueValidator
{
    private const int MaxItemLength = 500;

    public bool Accepts(FieldKind kind) => kind == FieldKind.TextList;

    public string? Problem(ContactFieldDefinition field, JsonElement value)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > field.MaxItems)
        {
            return $"must be a list of up to {field.MaxItems} texts";
        }

        return value.EnumerateArray().All(i => i.ValueKind == JsonValueKind.String && i.GetString() is { Length: > 0 and <= MaxItemLength })
            ? null
            : $"must hold only texts of 1 to {MaxItemLength} characters";
    }
}
