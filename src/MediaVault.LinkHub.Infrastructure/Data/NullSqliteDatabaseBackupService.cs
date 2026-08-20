using MediaVault.LinkHub.Application.Models.Database;
using MediaVault.LinkHub.Application.Services;

namespace MediaVault.LinkHub.Infrastructure.Data;

/// <summary>No-op para pruebas con SQLite en memoria.</summary>
public sealed class NullSqliteDatabaseBackupService : ISqliteDatabaseBackupService
{
    public string GetBackupDirectory() => string.Empty;

    public string GetDatabasePath() => string.Empty;

    public Task<DatabaseBackupResult> CreateBackupAsync(
        string reason,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new DatabaseBackupResult
        {
            Skipped = true,
            Message = "Backup deshabilitado."
        });

    public Task<DatabaseBackupResult> EnsureRecentBackupAsync(
        TimeSpan maxAge,
        string reason = "scheduled",
        CancellationToken cancellationToken = default) =>
        CreateBackupAsync(reason, cancellationToken);

    public IReadOnlyList<DatabaseBackupInfo> ListBackups() => Array.Empty<DatabaseBackupInfo>();

    public Task StageRestoreAsync(string backupFilePath, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public bool TryApplyPendingRestore(out string? message)
    {
        message = null;
        return false;
    }
}
