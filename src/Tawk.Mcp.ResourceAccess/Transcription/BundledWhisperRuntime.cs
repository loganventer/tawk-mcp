using System.Reflection;
using Tawk.Mcp.Core.Transcription;
using Whisper.net.LibraryLoader;

namespace Tawk.Mcp.ResourceAccess.Transcription;

/// <summary>
/// A published tawk-mcp is one file, and its program assembly carries the native libraries for its own
/// platform as resources (only the program is built knowing the platform). They
/// are unpacked into a private folder beside the models, named for this build so an update unpacks afresh,
/// and Whisper is pointed at them. The same on Linux, macOS and Windows.
/// </summary>
public sealed class BundledWhisperRuntime(Assembly program, TranscriptionOptions options) : IWhisperRuntime
{
    public const string Prefix = "whisper-native/";

    private readonly Lock _gate = new();
    private bool _done;

    public void Ensure()
    {
        lock (_gate)
        {
            if (_done)
            {
                return;
            }

            var assembly = program;
            var bundled = assembly.GetManifestResourceNames().Where(n => n.StartsWith(Prefix, StringComparison.Ordinal)).ToList();
            if (bundled.Count > 0)
            {
                var folder = Path.Combine(Path.GetDirectoryName(options.ModelDirectory.TrimEnd(Path.DirectorySeparatorChar)) ?? options.ModelDirectory, "native", Build(assembly));
                try
                {
                    Unpack(assembly, bundled, folder);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw new TranscriptionException("the transcriber's own files could not be unpacked", ex);
                }

                // Whisper looks in the folder of this path for runtimes/<platform>-<architecture>, which is
                // how the libraries were unpacked; the file name itself is never opened.
                RuntimeOptions.LibraryPath = Path.Combine(folder, "whisper");
            }

            _done = true;
        }
    }

    private static string Build(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Replace('+', '-') is { Length: > 0 } version
            ? string.Concat(version.Where(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-'))
            : "unknown";

    private static void Unpack(Assembly assembly, List<string> names, string folder)
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(folder);
        }
        else
        {
            Directory.CreateDirectory(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        foreach (var name in names)
        {
            using var resource = assembly.GetManifestResourceStream(name)!;

            // The name is <platform>-<architecture>/<file>, as the package lays it out.
            var parts = name[Prefix.Length..].Split('/');
            if (parts.Length != 2 || parts.Any(part => part.Length == 0 || part.StartsWith('.')))
            {
                continue;
            }

            var target = Path.Combine(folder, "runtimes", parts[0]);
            Directory.CreateDirectory(target);
            var path = Path.Combine(target, parts[1]);

            // A file of the right size is already unpacked, perhaps by another tawk-mcp that is using it now.
            if (File.Exists(path) && new FileInfo(path).Length == resource.Length)
            {
                continue;
            }

            var partial = path + "." + Environment.ProcessId + ".part";
            using (var file = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                resource.CopyTo(file);
            }

            File.Move(partial, path, overwrite: true);
        }
    }
}
