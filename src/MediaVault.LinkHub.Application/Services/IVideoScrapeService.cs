using MediaVault.LinkHub.Application.Models.Scraping;
using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// Orquesta scraping de videos a partir de un <see cref="ActressLink"/> y persiste resultados.
/// </summary>
public interface IVideoScrapeService
{
    /// <summary>
    /// Ejecuta el scrape del enlace y hace upsert de videos (nuevos = IsNew; ya conocidos se actualizan como vistos).
    /// </summary>
    Task<VideoScrapeResult> ScrapeAndPersistAsync(int actressLinkId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScrapedVideo>> GetPersistedByLinkAsync(int actressLinkId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScrapedVideo>> GetPersistedByActressAsync(int actressId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca como vistos (<c>IsNew = false</c>) los videos scrapeados del enlace.
    /// </summary>
    Task MarkVideosSeenAsync(int actressLinkId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza <see cref="ScrapedVideo.PreviewUrl"/> emparejando por <c>SourceUrl</c> (normalizada).
    /// </summary>
    Task<int> UpdatePreviewUrlsAsync(
        int actressLinkId,
        IReadOnlyDictionary<string, string> sourceUrlToPreviewUrl,
        CancellationToken cancellationToken = default);
}
