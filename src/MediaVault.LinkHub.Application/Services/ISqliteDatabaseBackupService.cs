using MediaVault.LinkHub.Application.Models.Database;

namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// Respaldo y restauración del SQLite local (estadísticas, tags, rankings, links).
/// </summary>
public interface ISqliteDatabaseBackupService
{
    string GetBackupDirectory();

    string GetDatabasePath();

    /// <summary>Crea una copia consistente con VACUUM INTO y rota respaldos antiguos.</summary>
    Task<DatabaseBackupResult> CreateBackupAsync(string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Si no hay un respaldo más reciente que <paramref name="maxAge"/>, crea uno.
    /// </summary>
    Task<DatabaseBackupResult> EnsureRecentBackupAsync(
        TimeSpan maxAge,
        string reason = "scheduled",
        CancellationToken cancellationToken = default);

    IReadOnlyList<DatabaseBackupInfo> ListBackups();

    /// <summary>
    /// Programa restauración al próximo arranque (reemplazo seguro del .db con la app cerrada de conexiones).
    /// </summary>
    Task StageRestoreAsync(string backupFilePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aplica restauración pendiente si existe. Debe invocarse antes de abrir el DbContext.
    /// </summary>
    bool TryApplyPendingRestore(out string? message);
}
