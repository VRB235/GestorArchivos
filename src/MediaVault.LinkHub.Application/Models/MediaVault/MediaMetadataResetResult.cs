namespace MediaVault.LinkHub.Application.Models.MediaVault;

/// <summary>
/// Resultado de restablecer metadatos de seguimiento en Media Vault.
/// </summary>
public sealed class MediaMetadataResetResult
{
    public int FilesUpdated { get; init; }

    public int CategoryLinksRemoved { get; init; }

    public int CategoriesDeleted { get; init; }

    public int ActressLinksRemoved { get; init; }

    public int ActressesDeleted { get; init; }

    public int ProducerLinksRemoved { get; init; }

    public int ProducersDeleted { get; init; }

    /// <summary>Ruta del respaldo creado antes del reset, si aplica.</summary>
    public string? BackupFilePath { get; init; }

    public bool HasChanges =>
        FilesUpdated > 0
        || CategoryLinksRemoved > 0
        || CategoriesDeleted > 0
        || ActressLinksRemoved > 0
        || ActressesDeleted > 0
        || ProducerLinksRemoved > 0
        || ProducersDeleted > 0;
}
