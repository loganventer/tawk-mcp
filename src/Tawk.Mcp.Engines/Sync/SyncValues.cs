using System.Globalization;
using System.Text;

namespace Tawk.Mcp.Engines.Sync;

/// <summary>Writes a row's values as text in one fixed way, for comparing rows and for the digest.</summary>
internal static class SyncValues
{
    public static string Write(IReadOnlyList<object?> values)
    {
        var text = new StringBuilder();
        foreach (var value in values)
        {
            switch (value)
            {
                case null:
                    text.Append("N;");
                    break;
                case long whole:
                    text.Append('I').Append(whole.ToString(CultureInfo.InvariantCulture)).Append(';');
                    break;
                case double real:
                    text.Append('R').Append(real.ToString("R", CultureInfo.InvariantCulture)).Append(';');
                    break;
                case byte[] bytes:
                    text.Append('B').Append(Convert.ToBase64String(bytes)).Append(';');
                    break;
                default:
                    var s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                    text.Append('S').Append(s.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(s).Append(';');
                    break;
            }
        }

        return text.ToString();
    }
}
