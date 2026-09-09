using MediaVault.LinkHub.Application.Models.Scraping;

namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// Dry-run de selectores CSS contra una URL de muestra (sin persistir).
/// </summary>
public interface IVideoScrapeProbeService
{
    /// <summary>
    /// Ejecuta un scrape de como máximo 1 página y devuelve el outcome (log + candidatos).
    /// </summary>
    Task<VideoPageScrapeOutcome> ProbeAsync(
        string url,
        VideoScrapeHints hints,
        string? scraperKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resuelve una URL de muestra para un WebLink (prioriza ActressLink / ProducerLink guardados).
    /// </summary>
    Task<string?> ResolveSampleUrlAsync(
        int webLinkId,
        bool preferActressProfile,
        CancellationToken cancellationToken = default);
}
