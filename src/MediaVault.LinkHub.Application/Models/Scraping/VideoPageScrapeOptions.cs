namespace MediaVault.LinkHub.Application.Models.Scraping;

/// <summary>
/// Opciones de ejecución del scraper (paginación incremental, URLs ya conocidas).
/// </summary>
public sealed class VideoPageScrapeOptions
{
    public static VideoPageScrapeOptions Default { get; } = new();

    /// <summary>
    /// Claves normalizadas de URLs ya persistidas. Si <see cref="StopWhenPageHasNoNewVideos"/>
    /// es true, se deja de paginar cuando una página no aporta ninguna URL nueva.
    /// </summary>
    public IReadOnlySet<string>? KnownUrlKeys { get; init; }

    /// <summary>
    /// Detiene la paginación si una página completa solo contiene videos ya obtenidos.
    /// </summary>
    public bool StopWhenPageHasNoNewVideos { get; init; }
}
