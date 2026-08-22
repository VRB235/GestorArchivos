namespace MediaVault.LinkHub.Application.Models.Scraping;

/// <summary>
/// Resultado de un scraper de página, con candidatos y log de diagnóstico.
/// </summary>
public sealed class VideoPageScrapeOutcome
{
    public IReadOnlyList<ScrapedVideoCandidate> Items { get; init; } = [];

    /// <summary>
    /// Líneas de diagnóstico (qué selectores coincidieron, tamaños HTTP, omisiones, etc.).
    /// </summary>
    public IReadOnlyList<string> Log { get; init; } = [];

    public int PagesFetched { get; init; }
}
