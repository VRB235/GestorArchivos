using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using MediaVault.LinkHub.Application.Models.Database;
using MediaVault.LinkHub.Application.Services;

using Microsoft.Data.Sqlite;

namespace MediaVault.LinkHub.Infrastructure.Data;

public sealed class SqliteDatabaseBackupService : ISqliteDatabaseBackupService
{
    public const string BackupsFolderName = "Backups";
    public const string PendingRestoreFileName = "pending-restore.json";
    public const int DefaultMaxBackups = 14;

    private static readonly Regex UnsafeReasonChars = new("[^a-zA-Z0-9_-]+", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _databasePath;
    private readonly string _backupDirectory;
    private readonly string _appDataDirectory;
    private readonly int _maxBackups;

    public SqliteDatabaseBackupService(
        string? databasePath = null,
        string? backupDirectory = null,
        int maxBackups = DefaultMaxBackups,
        string? appDataDirectory = null)
    {
        _appDataDirectory = appDataDirectory ?? SqliteDatabasePathProvider.GetAppDataDirectory();
        Directory.CreateDirectory(_appDataDirectory);
        _databasePath = databasePath ?? Path.Combine(_appDataDirectory, SqliteDatabasePathProvider.DatabaseFileName);
        _backupDirectory = backupDirectory
            ?? Path.Combine(_appDataDirectory, BackupsFolderName);
        _maxBackups = Math.Max(3, maxBackups);
        Directory.CreateDirectory(_backupDirectory);
    }

    public string GetBackupDirectory() => _backupDirectory;

    public string GetDatabasePath() => _databasePath;

    public async Task<DatabaseBackupResult> CreateBackupAsync(
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (IsInMemoryOrMissing())
        {
            return new DatabaseBackupResult
            {
                Skipped = true,
                Message = "No hay archivo SQLite en disco que respaldar."
            };
        }

        Directory.CreateDirectory(_backupDirectory);
        var safeReason = SanitizeReason(reason);
        var fileName =
            $"{Path.GetFileNameWithoutExtension(SqliteDatabasePathProvider.DatabaseFileName)}_" +
            $"{DateTime.Now:yyyyMMdd_HHmmss}_{safeReason}.db";
        var destinationPath = Path.Combine(_backupDirectory, fileName);

        await using (var connection = new SqliteConnection(SqliteDatabasePathProvider.BuildConnectionString(_databasePath)))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            // Copia consistente aunque haya WAL activo.
            command.CommandText = "VACUUM INTO $dest";
            command.Parameters.AddWithValue("$dest", destinationPath);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        SqliteConnection.ClearAllPools();
        PruneOldBackups();

        return new DatabaseBackupResult
        {
            Created = true,
            BackupFilePath = destinationPath,
            Message = $"Respaldo creado: {fileName}"
        };
    }

    public async Task<DatabaseBackupResult> EnsureRecentBackupAsync(
        TimeSpan maxAge,
        string reason = "scheduled",
        CancellationToken cancellationToken = default)
    {
        var latest = ListBackups().FirstOrDefault();
        if (latest is not null && DateTime.UtcNow - latest.CreatedUtc <= maxAge)
        {
            return new DatabaseBackupResult
            {
                Skipped = true,
                BackupFilePath = latest.FilePath,
                Message = $"Respaldo reciente ya existe ({latest.FileName})."
            };
        }

        return await CreateBackupAsync(reason, cancellationToken).ConfigureAwait(false);
    }

    public IReadOnlyList<DatabaseBackupInfo> ListBackups()
    {
        if (!Directory.Exists(_backupDirectory))
            return Array.Empty<DatabaseBackupInfo>();

        return Directory.EnumerateFiles(_backupDirectory, "*.db", SearchOption.TopDirectoryOnly)
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new DatabaseBackupInfo
                {
                    FilePath = path,
                    FileName = info.Name,
                    CreatedUtc = info.CreationTimeUtc,
                    SizeBytes = info.Length
                };
            })
            .OrderByDescending(item => item.CreatedUtc)
            .ToList();
    }

    public Task StageRestoreAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(backupFilePath) || !File.Exists(backupFilePath))
            throw new FileNotFoundException("No se encontró el respaldo indicado.", backupFilePath);

        var fullBackup = Path.GetFullPath(backupFilePath);
        var backupRoot = Path.GetFullPath(_backupDirectory);
        if (!fullBackup.StartsWith(backupRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Path.GetDirectoryName(fullBackup), backupRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Solo se pueden restaurar respaldos de la carpeta Backups de la app.");
        }

        var pendingPath = GetPendingRestorePath();
        var payload = new PendingRestoreDocument
        {
            BackupFilePath = fullBackup,
            StagedUtc = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        File.WriteAllText(pendingPath, json, Encoding.UTF8);

        return Task.CompletedTask;
    }

    public bool TryApplyPendingRestore(out string? message)
    {
        message = null;
        var pendingPath = GetPendingRestorePath();
        if (!File.Exists(pendingPath))
            return false;

        try
        {
            var json = File.ReadAllText(pendingPath, Encoding.UTF8);
            var pending = JsonSerializer.Deserialize<PendingRestoreDocument>(json, JsonOptions);
            if (pending is null || string.IsNullOrWhiteSpace(pending.BackupFilePath) || !File.Exists(pending.BackupFilePath))
            {
                File.Delete(pendingPath);
                message = "Respaldo pendiente inválido; se descartó la restauración.";
                return false;
            }

            // Seguridad: copiar el estado actual antes de sobrescribir.
            if (File.Exists(_databasePath))
            {
                var preRestoreName =
                    $"{Path.GetFileNameWithoutExtension(SqliteDatabasePathProvider.DatabaseFileName)}_" +
                    $"{DateTime.Now:yyyyMMdd_HHmmss}_pre-restore.db";
                var preRestorePath = Path.Combine(_backupDirectory, preRestoreName);
                Directory.CreateDirectory(_backupDirectory);
                File.Copy(_databasePath, preRestorePath, overwrite: true);
            }

            // Eliminar WAL/SHM huérfanos para no mezclar con el archivo restaurado.
            TryDelete(_databasePath + "-wal");
            TryDelete(_databasePath + "-shm");
            File.Copy(pending.BackupFilePath, _databasePath, overwrite: true);
            File.Delete(pendingPath);
            SqliteConnection.ClearAllPools();

            message = $"Base restaurada desde {Path.GetFileName(pending.BackupFilePath)}.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Falló la restauración pendiente: {ex.Message}";
            return false;
        }
    }

    private bool IsInMemoryOrMissing()
    {
        if (string.IsNullOrWhiteSpace(_databasePath))
            return true;

        if (_databasePath.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
            return true;

        return !File.Exists(_databasePath);
    }

    private void PruneOldBackups()
    {
        var backups = ListBackups();
        foreach (var obsolete in backups.Skip(_maxBackups))
        {
            try
            {
                File.Delete(obsolete.FilePath);
            }
            catch
            {
                // best effort
            }
        }
    }

    private string GetPendingRestorePath() =>
        Path.Combine(_appDataDirectory, PendingRestoreFileName);

    private static string SanitizeReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return "manual";

        var cleaned = UnsafeReasonChars.Replace(reason.Trim(), "-");
        if (cleaned.Length > 40)
            cleaned = cleaned[..40];

        return string.IsNullOrWhiteSpace(cleaned) ? "manual" : cleaned.ToLowerInvariant();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }

    private sealed class PendingRestoreDocument
    {
        public string BackupFilePath { get; set; } = string.Empty;

        public DateTime StagedUtc { get; set; }
    }
}
