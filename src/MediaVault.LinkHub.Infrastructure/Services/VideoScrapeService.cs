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

    public async Task<VideoScrapeResult> ScrapeAndPersistAsync(
        int actressLinkId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var link = await context.ActressLinks
            .FirstOrDefaultAsync(item => item.Id == actressLinkId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"No se encontró el enlace con Id {actressLinkId}.");

        if (link.Action != ActressLinkAction.Scrape)
            throw new InvalidOperationException("El enlace no está configurado para scraping.");

        var hints = DeserializeHints(link.ScrapeHintsJson);
        var scraperKey = string.IsNullOrWhiteSpace(link.ScraperKey)
            ? CssListVideoPageScraper.ScraperKey
            : link.ScraperKey.Trim();

        if (!_scrapers.TryGetValue(scraperKey, out var scraper))
        {
            throw new InvalidOperationException(
                $"No hay scraper registrado para la clave «{scraperKey}». Use «{CssListVideoPageScraper.ScraperKey}» o registre uno especializado.");
        }

        var outcome = await scraper
            .ScrapeAsync(link.Url, hints, cancellationToken)
            .ConfigureAwait(false);

        var candidates = outcome.Items;
        var warnings = new List<string>();
        if (candidates.Count == 0)
            warnings.Add("El scraper no encontró videos. Revise el log de diagnóstico y los selectores CSS.");

        var previous = await context.ScrapedVideos
            .Where(video => video.ActressLinkId == actressLinkId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byUrl = previous
            .GroupBy(video => NormalizeUrlKey(video.SourceUrl), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resultItems = new List<ScrapedVideoCandidate>();

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
            }
            else
            {
                context.ScrapedVideos.Add(new ScrapedVideo
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
                    ScrapedAt = now,
                    IsNew = true,
                    ExtraJson = candidate.Extra is { Count: > 0 }
                        ? JsonSerializer.Serialize(candidate.Extra, JsonOptions)
                        : null
                });
            }
        }

        link.LastScrapedAt = now;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new VideoScrapeResult
        {
            ActressLinkId = link.Id,
            ActressId = link.ActressId,
            SourceUrl = link.Url,
            Items = resultItems,
            Warnings = warnings,
            DiagnosticLog = outcome.Log,
            PagesFetched = outcome.PagesFetched
        };
    }

    public async Task MarkVideosSeenAsync(
        int actressLinkId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var videos = await context.ScrapedVideos
            .Where(video => video.ActressLinkId == actressLinkId && video.IsNew)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (videos.Count == 0)
            return;

        foreach (var video in videos)
            video.IsNew = false;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task<int> UpdatePreviewUrlsAsync(
        int actressLinkId,
        IReadOnlyDictionary<string, string> sourceUrlToPreviewUrl,
        CancellationToken cancellationToken = default)
    {
        if (sourceUrlToPreviewUrl.Count == 0)
            return 0;

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var videos = await context.ScrapedVideos
            .Where(video => video.ActressLinkId == actressLinkId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

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

    private static VideoScrapeHints DeserializeHints(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("El enlace no tiene ScrapeHintsJson.");

        try
        {
            return JsonSerializer.Deserialize<VideoScrapeHints>(json, JsonOptions)
                ?? throw new InvalidOperationException("ScrapeHintsJson inválido.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("ScrapeHintsJson no es JSON válido.", ex);
        }
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
}
