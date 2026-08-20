namespace MediaVault.LinkHub.Application.Models.Database;

public sealed class DatabaseBackupInfo
{
    public required string FilePath { get; init; }

    public required string FileName { get; init; }

    public DateTime CreatedUtc { get; init; }

    public long SizeBytes { get; init; }
}
