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
    /// Sitio de Link Manager del que se reutiliza logo/categoría.
    /// La <see cref="Url"/> de este enlace es la específica de la actriz.
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
    /// JSON de <c>VideoScrapeHints</c>: selectores y opciones para scrapear esta página.
    /// Obligatorio en la práctica cuando <see cref="Action"/> es <see cref="ActressLinkAction.Scrape"/>.
    /// </summary>
    public string? ScrapeHintsJson { get; set; }

    /// <summary>
    /// Clave opcional de scraper especializado (p. ej. «css-list», «site-xyz»).
    /// Si es null/vacío se usa el scraper CSS genérico guiado por hints.
    /// </summary>
    public string? ScraperKey { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastScrapedAt { get; set; }

    public ICollection<ScrapedVideo> ScrapedVideos { get; set; } = [];
}
