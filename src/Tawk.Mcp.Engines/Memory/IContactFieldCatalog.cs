using System.Text.Json;
using Tawk.Mcp.Core.Memory;

namespace Tawk.Mcp.Engines.Memory;

public interface IContactFieldCatalog
{
    IReadOnlyList<ContactFieldDefinition> All { get; }

    ContactFieldDefinition? Find(string name);

    /// <summary>Checks the field exists, the value fits it, and the source is allowed to set it.</summary>
    FieldCheck Check(string name, JsonElement value, FactSource source);
}
