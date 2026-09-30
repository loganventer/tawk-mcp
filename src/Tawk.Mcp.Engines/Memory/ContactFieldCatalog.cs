using System.Text.Json;
using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

/// <summary>Knows every field a profile may hold. New fields are new definitions; new value kinds are new validators.</summary>
public sealed class ContactFieldCatalog(IEnumerable<ContactFieldDefinition> fields, IEnumerable<IFieldValueValidator> validators) : IContactFieldCatalog
{
    private readonly IReadOnlyList<ContactFieldDefinition> _fields = fields.ToList();
    private readonly IReadOnlyList<IFieldValueValidator> _validators = validators.ToList();

    public IReadOnlyList<ContactFieldDefinition> All => _fields;

    public ContactFieldDefinition? Find(string name) =>
        _fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

    public FieldCheck Check(string name, JsonElement value, FactSource source)
    {
        var field = Find(name);
        if (field is null)
        {
            return new FieldCheck(null, $"{name} is not a profile field. list_contact_fields shows them all; put anything else in a note.");
        }

        if (field.Sensitive && source != FactSource.User)
        {
            return new FieldCheck(field, $"{field.Name} is sensitive and only the user may set it, never an inference or an import.");
        }

        if (!field.Inferable && source is FactSource.Inferred or FactSource.Imported)
        {
            return new FieldCheck(field, $"{field.Name} can only be stated by the user or the contact, not inferred.");
        }

        var validator = _validators.FirstOrDefault(v => v.Accepts(field.Kind));
        var problem = validator is null ? $"{field.Name} has no validator for {field.Kind}." : validator.Problem(field, value);
        return new FieldCheck(field, problem is null ? null : $"{field.Name} {problem}.");
    }
}
