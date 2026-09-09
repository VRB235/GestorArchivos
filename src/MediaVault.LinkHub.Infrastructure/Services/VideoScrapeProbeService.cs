using MediaVault.LinkHub.Application.Models.Scraping;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Infrastructure.Data;
using MediaVault.LinkHub.Infrastructure.Scraping;
using Microsoft.EntityFrameworkCore;

namespace MediaVault.LinkHub.Infrastructure.Services;

public sealed class VideoScrapeProbeService : IVideoScrapeProbeService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IReadOnlyDictionary<string, IVideoPageScraper> _scrapers;

    public VideoScrapeProbeService(
        IDbContextFactory<AppDbContext> contextFactory,
        IEnumerable<IVideoPageScraper> scrapers)
    {
        _contextFactory = contextFactory;
        _scrapers = scrapers.ToDictionary(scraper => scraper.Key, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<VideoPageScrapeOutcome> ProbeAsync(
        string url,
        VideoScrapeHints hints,
        string? scraperKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentNullException.ThrowIfNull(hints);

        var key = string.IsNullOrWhiteSpace(scraperKey) ? CssListVideoPageScraper.ScraperKey : scraperKey.Trim();
        if (!_scrapers.TryGetValue(key, out var scraper))
            throw new InvalidOperationException($"No hay scraper registrado para la clave «{key}».");

        var probeHints = CloneHints(hints);
        probeHints.MaxPages = 1;

        return await scraper
            .ScrapeAsync(url, probeHints, VideoPageScrapeOptions.Default, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<string?> ResolveSampleUrlAsync(
        int webLinkId,
        bool preferActressProfile,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        if (preferActressProfile)
        {
            var actressUrl = await context.ActressLinks
                .AsNoTracking()
                .Where(link => link.WebLinkId == webLinkId && link.Url != null && link.Url != "")
                .OrderByDescending(link => link.LastScrapedAt)
                .Select(link => link.Url)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(actressUrl))
                return actressUrl;
        }
        else
        {
            var producerUrl = await context.ProducerLinks
                .AsNoTracking()
                .Where(link => link.WebLinkId == webLinkId && link.Url != null && link.Url != "")
                .OrderByDescending(link => link.LastScrapedAt)
                .Select(link => link.Url)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(producerUrl))
                return producerUrl;
        }

        return await context.WebLinks
            .AsNoTracking()
            .Where(link => link.Id == webLinkId)
            .Select(link => link.Url)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static VideoScrapeHints CloneHints(VideoScrapeHints hints) =>
        new()
        {
            ListItemSelector = hints.ListItemSelector,
            TitleSelector = hints.TitleSelector,
            UrlSelector = hints.UrlSelector,
            UrlAttribute = hints.UrlAttribute,
            ThumbnailSelector = hints.ThumbnailSelector,
            ThumbnailAttribute = hints.ThumbnailAttribute,
            PreviewSelector = hints.PreviewSelector,
            PreviewAttribute = hints.PreviewAttribute,
            PreviewHoverSelector = hints.PreviewHoverSelector,
            PreviewHoverWaitMs = hints.PreviewHoverWaitMs,
            CodeSelector = hints.CodeSelector,
            DateSelector = hints.DateSelector,
            DateFormat = hints.DateFormat,
            DurationSelector = hints.DurationSelector,
            NextPageSelector = hints.NextPageSelector,
            MaxPages = hints.MaxPages,
            WaitMs = hints.WaitMs,
            ForceBrowser = hints.ForceBrowser,
            PreferBrowserFetch = hints.PreferBrowserFetch,
            UserAgent = hints.UserAgent,
            ExtraHeaders = hints.ExtraHeaders is null
                ? null
                : new Dictionary<string, string>(hints.ExtraHeaders, StringComparer.OrdinalIgnoreCase),
            BaseUrl = hints.BaseUrl
        };
}
