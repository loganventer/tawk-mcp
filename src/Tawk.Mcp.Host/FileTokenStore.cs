using System.Security.Cryptography;

namespace Tawk.Mcp.Host;

/// <summary>
/// Keeps the bearer token in a file readable only by the user (0600, in a 0700 folder).
/// The token is 32 random bytes in unpadded base64url.
/// </summary>
public sealed class FileTokenStore(string path) : ITokenStore
{
    private readonly Lock _gate = new();

    public static string DefaultPath(Func<string, string?> environment, string home)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var config = environment("XDG_CONFIG_HOME");
        var root = string.IsNullOrWhiteSpace(config) ? Path.Combine(home, ".config") : config;
        return Path.Combine(root, "tawk-mcp", "token");
    }

    public static string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public string GetOrCreate()
    {
        lock (_gate)
        {
            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path).Trim();
                if (existing.Length >= 32)
                {
                    return existing;
                }
            }

            var folder = Path.GetDirectoryName(Path.GetFullPath(path))!;
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(folder);
            }
            else
            {
                Directory.CreateDirectory(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            var token = NewToken();
            var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write, Share = FileShare.None };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (var stream = new FileStream(path, options))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(token);
                writer.Write('\n');
            }

            if (!OperatingSystem.IsWindows())
            {
                // An existing file keeps its old mode on create, so set it explicitly.
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            return token;
        }
    }

    public string Describe() => path;

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
