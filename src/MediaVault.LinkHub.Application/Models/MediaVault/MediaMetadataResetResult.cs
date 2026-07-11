namespace MediaVault.LinkHub.Application.Models.MediaVault;

/// <summary>
/// Resultado de restablecer metadatos de seguimiento en Media Vault.
/// </summary>
public sealed class MediaMetadataResetResult
{
    public int FilesUpdated { get; init; }

    public int CategoryLinksRemoved { get; init; }

    public int CategoriesDeleted { get; init; }

    public bool HasChanges =>
        FilesUpdated > 0 || CategoryLinksRemoved > 0 || CategoriesDeleted > 0;
}
