using MediaVault.LinkHub.Domain.Common;
using MediaVault.LinkHub.Domain.Enums;

namespace MediaVault.LinkHub.Domain.Entities;

/// <summary>
/// Enlace asociado a una productora: navegación externa o fuente de scraping del catálogo de videos.
/// </summary>
public class ProducerLink : EntityBase
{
    public int ProducerId { get; set; }

    public Producer Producer { get; set; } = null!;

    /// <summary>
    /// Sitio de Link Manager: logo, categoría y hints de scraping del sitio.
    /// Obligatorio cuando <see cref="Action"/> es <see cref="ActressLinkAction.Scrape"/>.
    /// La <see cref="Url"/> de este enlace es el listado de videos de la productora.
    /// </summary>
    public int? WebLinkId { get; set; }

    public WebLink? WebLink { get; set; }

    /// <summary>
    /// Etiqueta visible en la UI (p. ej. «Catálogo oficial», «Listado Brazzers»).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public ActressLinkAction Action { get; set; } = ActressLinkAction.Scrape;

    public string? Notes { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastScrapedAt { get; set; }

    public ICollection<ScrapedVideo> ScrapedVideos { get; set; } = [];
}
