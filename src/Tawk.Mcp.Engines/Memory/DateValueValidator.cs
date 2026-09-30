using System.Globalization;
using System.Text.Json;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>Takes a full date (2026-09-30) or, for birthdays without a known year, a month and day (--09-30).</summary>
public sealed class DateValueValidator : IFieldValueValidator
{
    public bool Accepts(FieldKind kind) => kind == FieldKind.Date;

    public string? Problem(ContactFieldDefinition field, JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String ? value.GetString()! : string.Empty;
        var full = DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        var yearless = text.StartsWith("--", StringComparison.Ordinal)
            && DateOnly.TryParseExact("2000" + text[1..], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        return full || yearless ? null : "must be a date as 2026-09-30, or --09-30 when the year is unknown";
    }
}
