using Microsoft.Data.Sqlite;

namespace Tawk.Mcp.ResourceAccess.Memory;

/// <summary>
/// Keeps the memory database in a file readable only by the user (0600, in a 0700 folder).
/// Nothing is created until the first time memory is used.
/// </summary>
public sealed class SqliteConnectionFactory(string path, ISchemaMigrator migrator) : ISqliteConnectionFactory, IDisposable
{
    private const UnixFileMode PrivateFile = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const UnixFileMode PrivateFolder = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private readonly SemaphoreSlim _ready = new(1, 1);
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = path,
        Mode = SqliteOpenMode.ReadWriteCreate,
    }.ToString();

    private bool _migrated;

    public string Path => path;

    public static string DefaultPath(Func<string, string?> environment, string home)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var data = environment("XDG_DATA_HOME");
        var root = string.IsNullOrWhiteSpace(data) ? System.IO.Path.Combine(home, ".local", "share") : data;
        return System.IO.Path.Combine(root, "tawk-mcp", "memory.db");
    }

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await SqliteCommands.ExecuteAsync(connection, "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;", cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public void Dispose() => _ready.Dispose();

    private async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        if (_migrated)
        {
            return;
        }

        await _ready.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_migrated)
            {
                return;
            }

            CreatePrivateFile();
            var connection = new SqliteConnection(_connectionString);
            await using (connection.ConfigureAwait(false))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                await SqliteCommands.ExecuteAsync(connection, "PRAGMA journal_mode = WAL;", cancellationToken).ConfigureAwait(false);
                await migrator.MigrateAsync(connection, cancellationToken).ConfigureAwait(false);
            }

            _migrated = true;
        }
        finally
        {
            _ready.Release();
        }
    }

    // SQLite gives its -wal and -shm files the database file's mode, so making this one private covers them too.
    private void CreatePrivateFile()
    {
        var folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!;
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(folder);
            return;
        }

        Directory.CreateDirectory(folder, PrivateFolder);
        var options = new FileStreamOptions { Mode = FileMode.OpenOrCreate, Access = FileAccess.Write, Share = FileShare.ReadWrite, UnixCreateMode = PrivateFile };
        using (new FileStream(path, options))
        {
        }

        // An existing file keeps its old mode on open, so set it explicitly.
        File.SetUnixFileMode(path, PrivateFile);
    }
}
