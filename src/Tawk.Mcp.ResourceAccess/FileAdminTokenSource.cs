using Tawk.Mcp.Core;

namespace Tawk.Mcp.ResourceAccess;

/// <summary>Reads the admin token from the file tawk keeps it in, each time it is needed.</summary>
public sealed class FileAdminTokenSource(AdminOptions options) : IAdminTokenSource
{
    private const int MaxLength = 256;

    public string? Read()
    {
        if (!options.Enabled)
        {
            return null;
        }

        try
        {
            var info = new FileInfo(options.TokenFile!);
            if (!info.Exists || info.Length > MaxLength)
            {
                return null;
            }

            var token = File.ReadAllText(info.FullName).Trim();
            return token.Length == 0 ? null : token;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
