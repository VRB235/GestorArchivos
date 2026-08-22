using System.Text.Json;

using MediaVault.LinkHub.Application.Models.Scraping;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Domain.Enums;
using MediaVault.LinkHub.Infrastructure.Data;
using MediaVault.LinkHub.Infrastructure.Launchers;
using Microsoft.EntityFrameworkCore;

namespace MediaVault.LinkHub.Infrastructure.Services;

public sealed class ActressLinkService : IActressLinkService
{
    private static readonly JsonSerializerOptions HintsJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public ActressLinkService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<ActressLink>> GetByActressIdAsync(
        int actressId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.ActressLinks
            .AsNoTracking()
            .Include(link => link.WebLink)
            .Where(link => link.ActressId == actressId)
            .OrderBy(link => link.SortOrder)
            .ThenBy(link => link.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ActressLink?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.ActressLinks
            .AsNoTracking()
            .Include(link => link.WebLink)
            .FirstOrDefaultAsync(link => link.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ActressLink> CreateAsync(
        int actressId,
        int? webLinkId,
        string title,
        string url,
        ActressLinkAction action,
        string? notes,
        string? scrapeHintsJson,
        string? scraperKey,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(title, url, action, notes, scrapeHintsJson, scraperKey);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        if (!await context.Actresses.AnyAsync(a => a.Id == actressId, cancellationToken).ConfigureAwait(false))
            throw new KeyNotFoundException($"No se encontró la actriz con Id {actressId}.");

        var resolvedWebLinkId = await ResolveWebLinkIdAsync(context, webLinkId, cancellationToken).ConfigureAwait(false);

        var maxSortOrder = await context.ActressLinks
            .Where(link => link.ActressId == actressId)
            .MaxAsync(link => (int?)link.SortOrder, cancellationToken)
            .ConfigureAwait(false) ?? -1;

        var entity = new ActressLink
        {
            ActressId = actressId,
            WebLinkId = resolvedWebLinkId,
            Title = normalized.Title,
            Url = normalized.Url,
            Action = normalized.Action,
            Notes = normalized.Notes,
            ScrapeHintsJson = normalized.ScrapeHintsJson,
            ScraperKey = normalized.ScraperKey,
            SortOrder = maxSortOrder + 1,
            CreatedAt = DateTime.UtcNow
        };

        context.ActressLinks.Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (await GetByIdAsync(entity.Id, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<ActressLink> UpdateAsync(
        int id,
        int? webLinkId,
        string title,
        string url,
        ActressLinkAction action,
        string? notes,
        string? scrapeHintsJson,
        string? scraperKey,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(title, url, action, notes, scrapeHintsJson, scraperKey);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.ActressLinks
            .FirstOrDefaultAsync(link => link.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"No se encontró el enlace con Id {id}.");

        entity.WebLinkId = await ResolveWebLinkIdAsync(context, webLinkId, cancellationToken).ConfigureAwait(false);
        entity.Title = normalized.Title;
        entity.Url = normalized.Url;
        entity.Action = normalized.Action;
        entity.Notes = normalized.Notes;
        entity.ScrapeHintsJson = normalized.ScrapeHintsJson;
        entity.ScraperKey = normalized.ScraperKey;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (await GetByIdAsync(entity.Id, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.ActressLinks
            .FirstOrDefaultAsync(link => link.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
            return;

        context.ActressLinks.Remove(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> OpenInBrowserAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.ActressLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(link => link.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is not null && BrowserLauncher.TryOpenInFirefox(entity.Url);
    }

    public async Task MarkScrapedAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.ActressLinks
            .FirstOrDefaultAsync(link => link.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
            return;

        entity.LastScrapedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int?> ResolveWebLinkIdAsync(
        AppDbContext context,
        int? webLinkId,
        CancellationToken cancellationToken)
    {
        if (webLinkId is null)
            return null;

        if (!await context.WebLinks.AnyAsync(link => link.Id == webLinkId.Value, cancellationToken).ConfigureAwait(false))
            throw new KeyNotFoundException($"No se encontró el WebLink con Id {webLinkId.Value}.");

        return webLinkId;
    }

    private static (
        string Title,
        string Url,
        ActressLinkAction Action,
        string? Notes,
        string? ScrapeHintsJson,
        string? ScraperKey) Normalize(
        string title,
        string url,
        ActressLinkAction action,
        string? notes,
        string? scrapeHintsJson,
        string? scraperKey)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El título del enlace es obligatorio.", nameof(title));

        var normalizedUrl = NormalizeUrl(url);
        var trimmedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        var trimmedKey = string.IsNullOrWhiteSpace(scraperKey) ? null : scraperKey.Trim();
        var hintsJson = NormalizeHintsJson(scrapeHintsJson, action);

        return (title.Trim(), normalizedUrl, action, trimmedNotes, hintsJson, trimmedKey);
    }

    private static string? NormalizeHintsJson(string? scrapeHintsJson, ActressLinkAction action)
    {
        if (string.IsNullOrWhiteSpace(scrapeHintsJson))
        {
            if (action == ActressLinkAction.Scrape)
            {
                throw new ArgumentException(
                    "Los enlaces de scraping requieren configuración de selectores.",
                    nameof(scrapeHintsJson));
            }

            return null;
        }

        try
        {
            var hints = JsonSerializer.Deserialize<VideoScrapeHints>(scrapeHintsJson, HintsJsonOptions)
                ?? throw new ArgumentException("ScrapeHintsJson inválido.", nameof(scrapeHintsJson));

            if (action == ActressLinkAction.Scrape
                && string.IsNullOrWhiteSpace(hints.ListItemSelector)
                && string.IsNullOrWhiteSpace(hints.TitleSelector))
            {
                throw new ArgumentException(
                    "Indique al menos ListItemSelector o TitleSelector en los hints de scraping.",
                    nameof(scrapeHintsJson));
            }

            return JsonSerializer.Serialize(hints, HintsJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("ScrapeHintsJson no es JSON válido.", nameof(scrapeHintsJson), ex);
        }
    }

    private static string NormalizeUrl(string url)
    {
        var trimmed = url.Trim();
        if (!trimmed.Contains("://", StringComparison.Ordinal))
            trimmed = $"https://{trimmed}";

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("La URL debe ser HTTP o HTTPS válida.", nameof(url));

        return uri.AbsoluteUri;
    }
}
