using MediaVault.LinkHub.Domain.Common;
using MediaVault.LinkHub.Domain.Enums;

namespace MediaVault.LinkHub.Domain.Entities;

/// <summary>
/// Enlace asociado a una actriz: navegación externa o fuente de scraping de videos.
/// </summary>
public class ActressLink : EntityBase
{
    public int ActressId { get; set; }

    public Actress Actress { get; set; } = null!;

    /// <summary>
    /// Sitio de Link Manager (productora): logo, categoría y hints de scraping del sitio.
    /// Obligatorio cuando <see cref="Action"/> es <see cref="ActressLinkAction.Scrape"/>.
    /// La <see cref="Url"/> de este enlace es la página específica de la actriz.
    /// </summary>
    public int? WebLinkId { get; set; }

    public WebLink? WebLink { get; set; }

    /// <summary>
    /// Etiqueta visible en la UI (p. ej. «Perfil oficial», «Listado JAVLibrary»).
    /// </summary>
    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public ActressLinkAction Action { get; set; } = ActressLinkAction.Browse;

    /// <summary>
    /// Notas libres para el usuario (cómo usar el sitio, credenciales, etc.).
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Legacy: los hints viven en <see cref="WebLink.ScrapeHintsJson"/>.
    /// Conservado solo por compatibilidad de esquema; no se escribe en nuevos guardados.
    /// </summary>
    public string? ScrapeHintsJson { get; set; }

    /// <summary>
    /// Legacy: la clave del scraper vive en <see cref="WebLink.ScraperKey"/>.
    /// </summary>
    public string? ScraperKey { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastScrapedAt { get; set; }

    public ICollection<ScrapedVideo> ScrapedVideos { get; set; } = [];
}
