using System.Globalization;
using System.Text.Json;

namespace Tawk.Mcp.Engines.Memory;

public sealed class NumberValueValidator : IFieldValueValidator
{
    public bool Accepts(FieldKind kind) => kind is FieldKind.Number or FieldKind.WholeNumber;

    public string? Problem(ContactFieldDefinition field, JsonElement value)
    {
        ArgumentNullException.ThrowIfNull(field);
        var range = string.Create(CultureInfo.InvariantCulture, $"{field.Min ?? double.MinValue} to {field.Max ?? double.MaxValue}");
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number))
        {
            return "must be a number from " + range;
        }

        if (field.Kind == FieldKind.WholeNumber && Math.Abs(number % 1) > double.Epsilon)
        {
            return "must be a whole number from " + range;
        }

        return number < (field.Min ?? double.MinValue) || number > (field.Max ?? double.MaxValue) ? "must be from " + range : null;
    }
}
