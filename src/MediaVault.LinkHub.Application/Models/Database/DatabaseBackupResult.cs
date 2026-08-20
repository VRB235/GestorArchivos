namespace MediaVault.LinkHub.Application.Models.Database;

public sealed class DatabaseBackupResult
{
    public bool Created { get; init; }

    public bool Skipped { get; init; }

    public string? BackupFilePath { get; init; }

    public string? Message { get; init; }
}
