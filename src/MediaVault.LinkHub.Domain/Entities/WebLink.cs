using MediaVault.LinkHub.Domain.Common;
using MediaVault.LinkHub.Domain.Enums;

namespace MediaVault.LinkHub.Domain.Entities;

/// <summary>
/// Enlace web gestionado por el módulo Link Manager.
/// </summary>
public class WebLink : EntityBase
{
    public string Nombre { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Ruta local al logo/icono del enlace (opcional).
    /// </summary>
    public string? LogoPath { get; set; }

    /// <summary>
    /// Zoom del logo en el tile (1 = ajuste por defecto; mayor = acercar / recortar más).
    /// </summary>
    public double LogoZoom { get; set; } = 1.0;

    /// <summary>
    /// Desplazamiento horizontal del logo (-1 izquierda … 1 derecha) dentro del recorte.
    /// </summary>
    public double LogoOffsetX { get; set; }

    /// <summary>
    /// Desplazamiento vertical del logo (-1 arriba … 1 abajo) dentro del recorte.
    /// </summary>
    public double LogoOffsetY { get; set; }

    public LinkCategory Categoria { get; set; } = LinkCategory.Oficial;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Fecha en que el usuario marcó haber visitado/revisado el sitio (no es timestamp de BD).
    /// </summary>
    public DateTime? FechaUltimaActualizacion { get; set; }

    /// <summary>
    /// Productoras/fuentes asociadas a este sitio.
    /// </summary>
    public ICollection<Producer> Producers { get; set; } = [];

    /// <summary>
    /// JSON de <c>VideoScrapeHints</c> para páginas de <b>actriz</b> (perfil/model).
    /// Compartido por todas las ActressLink de este sitio.
    /// </summary>
    public string? ScrapeHintsJson { get; set; }

    /// <summary>
    /// Clave del scraper para páginas de actriz (p. ej. «css-list»).
    /// </summary>
    public string? ScraperKey { get; set; }

    /// <summary>
    /// JSON de <c>VideoScrapeHints</c> para el <b>catálogo de productora</b> (listado general de videos).
    /// Independiente de los hints de actriz: el DOM suele ser distinto.
    /// </summary>
    public string? ProducerScrapeHintsJson { get; set; }

    /// <summary>
    /// Clave del scraper para catálogo de productora (p. ej. «css-list»).
    /// </summary>
    public string? ProducerScraperKey { get; set; }
}
