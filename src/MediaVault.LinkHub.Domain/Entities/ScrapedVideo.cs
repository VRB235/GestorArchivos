using MediaVault.LinkHub.Domain.Common;

namespace MediaVault.LinkHub.Domain.Entities;

/// <summary>
/// Metadatos de un video remoto obtenidos por scraping (distinto de <see cref="MediaFile"/> local).
/// </summary>
public class ScrapedVideo : EntityBase
{
    public int ActressLinkId { get; set; }

    public ActressLink ActressLink { get; set; } = null!;

    public int ActressId { get; set; }

    public Actress Actress { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// URL canónica del video o de su ficha en el sitio fuente.
    /// </summary>
    public string SourceUrl { get; set; } = string.Empty;

    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// URL de preview al hover (mp4/webm/gif/webp animado) si el sitio la expone en el HTML.
    /// </summary>
    public string? PreviewUrl { get; set; }

    /// <summary>
    /// True la primera vez que el scrape descubrió este video; se limpia al revisarlo en la UI.
    /// </summary>
    public bool IsNew { get; set; } = true;

    /// <summary>
    /// Código / ID de producto del sitio (si existe).
    /// </summary>
    public string? Code { get; set; }

    public string? DurationText { get; set; }

    public DateTime? PublishedAt { get; set; }

    public DateTime ScrapedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Campos adicionales serializados (JSON) que no caben en columnas fijas.
    /// </summary>
    public string? ExtraJson { get; set; }
}
