using System.Text.Json;

using MediaVault.LinkHub.Application.Models.Scraping;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Domain.Enums;
using MediaVault.LinkHub.Infrastructure.Data;
using MediaVault.LinkHub.Infrastructure.Scraping;
using Microsoft.EntityFrameworkCore;

namespace MediaVault.LinkHub.Infrastructure.Services;

public sealed class VideoScrapeService : IVideoScrapeService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IReadOnlyDictionary<string, IVideoPageScraper> _scrapers;

    public VideoScrapeService(
        IDbContextFactory<AppDbContext> contextFactory,
        IEnumerable<IVideoPageScraper> scrapers)
    {
        _contextFactory = contextFactory;
        _scrapers = scrapers.ToDictionary(scraper => scraper.Key, StringComparer.OrdinalIgnoreCase);
    }

    public Task<VideoScrapeResult> ScrapeAndPersistAsync(
        int actressLinkId,
        CancellationToken cancellationToken = default) =>
        ScrapeActressLinkAsync(actressLinkId, cancellationToken);

    public Task<VideoScrapeResult> ScrapeAndPersistByProducerLinkAsync(
        int producerLinkId,
        CancellationToken cancellationToken = default) =>
        ScrapeProducerLinkAsync(producerLinkId, cancellationToken);

    public async Task MarkVideosSeenAsync(
        int actressLinkId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await MarkSeenCoreAsync(context, video => video.ActressLinkId == actressLinkId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task MarkProducerVideosSeenAsync(
        int producerLinkId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await MarkSeenCoreAsync(context, video => video.ProducerLinkId == producerLinkId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ScrapedVideo>> GetPersistedByLinkAsync(
        int actressLinkId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.ScrapedVideos
            .AsNoTracking()
            .Where(video => video.ActressLinkId == actressLinkId)
            .OrderByDescending(video => video.ScrapedAt)
            .ThenBy(video => video.Title)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ScrapedVideo>> GetPersistedByProducerLinkAsync(
        int producerLinkId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.ScrapedVideos
            .AsNoTracking()
            .Where(video => video.ProducerLinkId == producerLinkId)
            .OrderByDescending(video => video.ScrapedAt)
            .ThenBy(video => video.Title)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ScrapedVideo>> GetPersistedByActressAsync(
        int actressId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.ScrapedVideos
            .AsNoTracking()
            .Where(video => video.ActressId == actressId)
            .OrderByDescending(video => video.ScrapedAt)
            .ThenBy(video => video.Title)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ScrapedVideo>> GetPersistedByProducerAsync(
        int producerId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.ScrapedVideos
            .AsNoTracking()
            .Where(video => video.ProducerId == producerId)
            .OrderByDescending(video => video.ScrapedAt)
            .ThenBy(video => video.Title)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> UpdatePreviewUrlsAsync(
        int actressLinkId,
        IReadOnlyDictionary<string, string> sourceUrlToPreviewUrl,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var videos = await context.ScrapedVideos
            .Where(video => video.ActressLinkId == actressLinkId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return await UpdatePreviewUrlsCoreAsync(context, videos, sourceUrlToPreviewUrl, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<VideoScrapeResult> ScrapeActressLinkAsync(
        int actressLinkId,
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var link = await context.ActressLinks
            .Include(item => item.WebLink)
            .FirstOrDefaultAsync(item => item.Id == actressLinkId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"No se encontró el enlace con Id {actressLinkId}.");

        ValidateScrapeLink(link.Action, link.WebLinkId, link.WebLink);

        var (hints, scraper) = ResolveActressHintsAndScraper(link.WebLink!);
        var previous = await context.ScrapedVideos
            .Where(video => video.ActressLinkId == actressLinkId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var outcome = await ExecuteScrapeAsync(
                link.Url,
                hints,
                link.LastScrapedAt,
                previous,
                scraper,
                cancellationToken)
            .ConfigureAwait(false);

        var persist = PersistCandidates(
            previous,
            outcome.Items,
            candidate => new ScrapedVideo
            {
                ActressLinkId = link.Id,
                ActressId = link.ActressId,
                Title = Truncate(candidate.Title, 500),
                SourceUrl = Truncate(candidate.SourceUrl, 2048),
                ThumbnailUrl = TruncateNullable(candidate.ThumbnailUrl, 2048),
                PreviewUrl = TruncateNullable(candidate.PreviewUrl, 2048),
                Code = TruncateNullable(candidate.Code, 100),
                DurationText = TruncateNullable(candidate.DurationText, 40),
                PublishedAt = candidate.PublishedAt,
                ScrapedAt = DateTime.UtcNow,
                IsNew = true,
                ExtraJson = candidate.Extra is { Count: > 0 }
                    ? JsonSerializer.Serialize(candidate.Extra, JsonOptions)
                    : null
            },
            (existing, candidate, now) =>
            {
                existing.Title = Truncate(candidate.Title, 500);
                existing.ThumbnailUrl = TruncateNullable(candidate.ThumbnailUrl, 2048)
                    ?? existing.ThumbnailUrl;
                existing.PreviewUrl = TruncateNullable(candidate.PreviewUrl, 2048)
                    ?? existing.PreviewUrl;
                existing.Code = TruncateNullable(candidate.Code, 100) ?? existing.Code;
                existing.DurationText = TruncateNullable(candidate.DurationText, 40)
                    ?? existing.DurationText;
                existing.PublishedAt = candidate.PublishedAt ?? existing.PublishedAt;
                existing.ScrapedAt = now;
                existing.IsNew = false;
                existing.ExtraJson = candidate.Extra is { Count: > 0 }
                    ? JsonSerializer.Serialize(candidate.Extra, JsonOptions)
                    : existing.ExtraJson;
            });

        foreach (var video in persist.NewEntities)
            context.ScrapedVideos.Add(video);

        link.LastScrapedAt = persist.Now;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new VideoScrapeResult
        {
            ActressLinkId = link.Id,
            ActressId = link.ActressId,
            SourceUrl = link.Url,
            Items = persist.ResultItems,
            Warnings = persist.Warnings,
            DiagnosticLog = outcome.Log,
            PagesFetched = outcome.PagesFetched
        };
    }

    private async Task<VideoScrapeResult> ScrapeProducerLinkAsync(
        int producerLinkId,
        CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var link = await context.ProducerLinks
            .Include(item => item.WebLink)
            .FirstOrDefaultAsync(item => item.Id == producerLinkId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"No se encontró el enlace con Id {producerLinkId}.");

        ValidateScrapeLink(link.Action, link.WebLinkId, link.WebLink);

        var (hints, scraper) = ResolveProducerHintsAndScraper(link.WebLink!);
        var previous = await context.ScrapedVideos
            .Where(video => video.ProducerLinkId == producerLinkId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var outcome = await ExecuteScrapeAsync(
                link.Url,
                hints,
                link.LastScrapedAt,
                previous,
                scraper,
                cancellationToken)
            .ConfigureAwait(false);

        var persist = PersistCandidates(
            previous,
            outcome.Items,
            candidate => new ScrapedVideo
            {
                ProducerLinkId = link.Id,
                ProducerId = link.ProducerId,
                Title = Truncate(candidate.Title, 500),
                SourceUrl = Truncate(candidate.SourceUrl, 2048),
                ThumbnailUrl = TruncateNullable(candidate.ThumbnailUrl, 2048),
                PreviewUrl = TruncateNullable(candidate.PreviewUrl, 2048),
                Code = TruncateNullable(candidate.Code, 100),
                DurationText = TruncateNullable(candidate.DurationText, 40),
                PublishedAt = candidate.PublishedAt,
                ScrapedAt = DateTime.UtcNow,
                IsNew = true,
                ExtraJson = candidate.Extra is { Count: > 0 }
                    ? JsonSerializer.Serialize(candidate.Extra, JsonOptions)
                    : null
            },
            (existing, candidate, now) =>
            {
                existing.Title = Truncate(candidate.Title, 500);
                existing.ThumbnailUrl = TruncateNullable(candidate.ThumbnailUrl, 2048)
                    ?? existing.ThumbnailUrl;
                existing.PreviewUrl = TruncateNullable(candidate.PreviewUrl, 2048)
                    ?? existing.PreviewUrl;
                existing.Code = TruncateNullable(candidate.Code, 100) ?? existing.Code;
                existing.DurationText = TruncateNullable(candidate.DurationText, 40)
                    ?? existing.DurationText;
                existing.PublishedAt = candidate.PublishedAt ?? existing.PublishedAt;
                existing.ScrapedAt = now;
                existing.IsNew = false;
                existing.ExtraJson = candidate.Extra is { Count: > 0 }
                    ? JsonSerializer.Serialize(candidate.Extra, JsonOptions)
                    : existing.ExtraJson;
            });

        foreach (var video in persist.NewEntities)
            context.ScrapedVideos.Add(video);

        link.LastScrapedAt = persist.Now;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new VideoScrapeResult
        {
            ProducerLinkId = link.Id,
            ProducerId = link.ProducerId,
            SourceUrl = link.Url,
            Items = persist.ResultItems,
            Warnings = persist.Warnings,
            DiagnosticLog = outcome.Log,
            PagesFetched = outcome.PagesFetched
        };
    }

    private static void ValidateScrapeLink(
        ActressLinkAction action,
        int? webLinkId,
        WebLink? webLink)
    {
        if (action != ActressLinkAction.Scrape)
            throw new InvalidOperationException("El enlace no está configurado para scraping.");

        if (webLinkId is null || webLink is null)
        {
            throw new InvalidOperationException(
                "Asocie el enlace a un sitio del Link Manager (productora) antes de scrapear.");
        }
    }

    private (VideoScrapeHints Hints, IVideoPageScraper Scraper) ResolveActressHintsAndScraper(WebLink webLink)
    {
        // Una sola configuración por sitio: todas las páginas de actriz comparten el mismo DOM.
        var hints = VideoScrapeHintsJson.DeserializeRequired(
            webLink.ScrapeHintsJson,
            "No hay selectores de scraping de actriz en el sitio (Link Manager → Scraping actriz).");

        var scraperKey = string.IsNullOrWhiteSpace(webLink.ScraperKey)
            ? CssListVideoPageScraper.ScraperKey
            : webLink.ScraperKey.Trim();

        return (hints, ResolveScraper(scraperKey));
    }

    private (VideoScrapeHints Hints, IVideoPageScraper Scraper) ResolveProducerHintsAndScraper(WebLink webLink)
    {
        var hints = VideoScrapeHintsJson.DeserializeRequired(
            webLink.ProducerScrapeHintsJson,
            "No hay selectores de scraping de catálogo de productora en el sitio (Link Manager → Scraping productora).");

        var scraperKey = string.IsNullOrWhiteSpace(webLink.ProducerScraperKey)
            ? CssListVideoPageScraper.ScraperKey
            : webLink.ProducerScraperKey.Trim();

        return (hints, ResolveScraper(scraperKey));
    }

    private IVideoPageScraper ResolveScraper(string scraperKey)
    {
        if (!_scrapers.TryGetValue(scraperKey, out var scraper))
        {
            throw new InvalidOperationException(
                $"No hay scraper registrado para la clave «{scraperKey}». Use «{CssListVideoPageScraper.ScraperKey}» o registre uno especializado.");
        }

        return scraper;
    }

    private async Task<VideoPageScrapeOutcome> ExecuteScrapeAsync(
        string url,
        VideoScrapeHints hints,
        DateTime? lastScrapedAt,
        IReadOnlyList<ScrapedVideo> previous,
        IVideoPageScraper scraper,
        CancellationToken cancellationToken)
    {
        var effectiveHints = PrepareHintsForRun(hints, lastScrapedAt);
        var knownKeys = previous
            .Select(video => NormalizeUrlKey(video.SourceUrl))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var options = new VideoPageScrapeOptions
        {
            KnownUrlKeys = knownKeys,
            StopWhenPageHasNoNewVideos = lastScrapedAt is not null
        };

        return await scraper
            .ScrapeAsync(url, effectiveHints, options, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Primera corrida: solo página 1. Corridas posteriores: MaxPages del sitio con parada temprana si no hay novedades.
    /// </summary>
    private static VideoScrapeHints PrepareHintsForRun(VideoScrapeHints hints, DateTime? lastScrapedAt)
    {
        if (lastScrapedAt is null)
        {
            return new VideoScrapeHints
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
                MaxPages = 1,
                WaitMs = hints.WaitMs,
                UserAgent = hints.UserAgent,
                ExtraHeaders = hints.ExtraHeaders,
                BaseUrl = hints.BaseUrl
            };
        }

        return hints;
    }

    private PersistOutcome PersistCandidates(
        IReadOnlyList<ScrapedVideo> previous,
        IReadOnlyList<ScrapedVideoCandidate> candidates,
        Func<ScrapedVideoCandidate, ScrapedVideo> createNew,
        Action<ScrapedVideo, ScrapedVideoCandidate, DateTime> updateExisting)
    {
        var warnings = new List<string>();
        if (candidates.Count == 0)
            warnings.Add("El scraper no encontró videos. Revise el log de diagnóstico y los selectores CSS.");

        var byUrl = previous
            .GroupBy(video => NormalizeUrlKey(video.SourceUrl), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resultItems = new List<ScrapedVideoCandidate>();
        var newEntities = new List<ScrapedVideo>();

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.SourceUrl))
            {
                warnings.Add($"Se omitió un ítem sin URL (título: {candidate.Title}).");
                continue;
            }

            var key = NormalizeUrlKey(candidate.SourceUrl);
            if (!seenKeys.Add(key))
                continue;

            resultItems.Add(candidate);

            if (byUrl.TryGetValue(key, out var existing))
            {
                updateExisting(existing, candidate, now);
            }
            else
            {
                newEntities.Add(createNew(candidate));
            }
        }

        return new PersistOutcome(now, resultItems, warnings, newEntities);
    }

    private static async Task MarkSeenCoreAsync(
        AppDbContext context,
        System.Linq.Expressions.Expression<Func<ScrapedVideo, bool>> predicate,
        CancellationToken cancellationToken)
    {
        var videos = await context.ScrapedVideos
            .Where(predicate)
            .Where(video => video.IsNew)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (videos.Count == 0)
            return;

        foreach (var video in videos)
            video.IsNew = false;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> UpdatePreviewUrlsCoreAsync(
        AppDbContext context,
        IReadOnlyList<ScrapedVideo> videos,
        IReadOnlyDictionary<string, string> sourceUrlToPreviewUrl,
        CancellationToken cancellationToken)
    {
        if (sourceUrlToPreviewUrl.Count == 0)
            return 0;

        var map = sourceUrlToPreviewUrl
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => NormalizeUrlKey(pair.Key), pair => pair.Value.Trim(), StringComparer.OrdinalIgnoreCase);

        var updated = 0;
        foreach (var video in videos)
        {
            var key = NormalizeUrlKey(video.SourceUrl);
            if (!map.TryGetValue(key, out var preview))
                continue;

            var next = Truncate(preview, 2048);
            if (string.Equals(video.PreviewUrl, next, StringComparison.Ordinal))
                continue;

            video.PreviewUrl = next;
            updated++;
        }

        if (updated > 0)
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return updated;
    }

    private static string NormalizeUrlKey(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return url.Trim().TrimEnd('/');

        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }

    private static string Truncate(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? TruncateNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return Truncate(value, maxLength);
    }

    private sealed record PersistOutcome(
        DateTime Now,
        List<ScrapedVideoCandidate> ResultItems,
        List<string> Warnings,
        List<ScrapedVideo> NewEntities);
}
