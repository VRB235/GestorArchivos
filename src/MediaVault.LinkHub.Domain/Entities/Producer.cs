using MediaVault.LinkHub.Domain.Common;

namespace MediaVault.LinkHub.Domain.Entities;

/// <summary>
/// Productora o fuente: asociada a enlaces web y, opcionalmente, a videos indexados.
/// </summary>
public class Producer : EntityBase
{
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public ICollection<MediaFile> MediaFiles { get; set; } = [];

    public ICollection<WebLink> WebLinks { get; set; } = [];
}
