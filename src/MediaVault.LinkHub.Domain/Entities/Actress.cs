using MediaVault.LinkHub.Domain.Common;

namespace MediaVault.LinkHub.Domain.Entities;

/// <summary>
/// Actriz gestionable para etiquetar videos indexados en Media Vault.
/// </summary>
public class Actress : EntityBase
{
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public ICollection<MediaFile> MediaFiles { get; set; } = [];
}
