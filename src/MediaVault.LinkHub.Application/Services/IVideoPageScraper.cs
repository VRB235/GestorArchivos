using MediaVault.LinkHub.Application.Models.Scraping;
using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// Contrato de un scraper concreto (genérico CSS o sitio especializado).
/// </summary>
public interface IVideoPageScraper
{
    /// <summary>
    /// Clave estable (p. ej. <c>css-list</c>). Coincide con <see cref="ActressLink.ScraperKey"/>.
    /// </summary>
    string Key { get; }

    Task<VideoPageScrapeOutcome> ScrapeAsync(
        string startUrl,
        VideoScrapeHints hints,
        CancellationToken cancellationToken = default);
}
