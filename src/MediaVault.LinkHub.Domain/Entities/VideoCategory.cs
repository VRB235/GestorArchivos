using MediaVault.LinkHub.Domain.Common;

namespace MediaVault.LinkHub.Domain.Entities;

/// <summary>
/// Categoría gestionable para clasificar archivos indexados en Media Vault.
/// </summary>
public class VideoCategory : EntityBase
{
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public ICollection<MediaFile> MediaFiles { get; set; } = [];
}
