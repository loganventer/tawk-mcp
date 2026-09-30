using System.Text.Json;

namespace Tawk.Mcp.Engines.Memory;

public sealed class ChoiceValueValidator : IFieldValueValidator
{
    public bool Accepts(FieldKind kind) => kind is FieldKind.Choice or FieldKind.ChoiceList;

    public string? Problem(ContactFieldDefinition field, JsonElement value)
    {
        ArgumentNullException.ThrowIfNull(field);
        var choices = field.Choices ?? [];
        var options = string.Join(", ", choices);
        bool Allowed(JsonElement item) =>
            item.ValueKind == JsonValueKind.String && choices.Contains(item.GetString()!, StringComparer.OrdinalIgnoreCase);

        if (field.Kind == FieldKind.Choice)
        {
            return Allowed(value) ? null : "must be one of: " + options;
        }

        return value.ValueKind == JsonValueKind.Array && value.GetArrayLength() <= field.MaxItems && value.EnumerateArray().All(Allowed)
            ? null
            : $"must be a list of up to {field.MaxItems} of: {options}";
    }
}
