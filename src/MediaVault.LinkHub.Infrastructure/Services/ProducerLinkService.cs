using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Domain.Enums;
using MediaVault.LinkHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MediaVault.LinkHub.Infrastructure.Services;

public sealed class ProducerLinkService : IProducerLinkService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly IBrowserLauncher _browserLauncher;

    public ProducerLinkService(
        IDbContextFactory<AppDbContext> contextFactory,
        IBrowserLauncher browserLauncher)
    {
        _contextFactory = contextFactory;
        _browserLauncher = browserLauncher;
    }

    public async Task<IReadOnlyList<ProducerLink>> GetByProducerIdAsync(
        int producerId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.ProducerLinks
            .AsNoTracking()
            .Include(link => link.WebLink)
            .Where(link => link.ProducerId == producerId)
            .OrderBy(link => link.SortOrder)
            .ThenBy(link => link.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ProducerLink?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.ProducerLinks
            .AsNoTracking()
            .Include(link => link.WebLink)
            .FirstOrDefaultAsync(link => link.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ProducerLink> CreateAsync(
        int producerId,
        int? webLinkId,
        string title,
        string url,
        ActressLinkAction action,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(title, url, action, notes);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        if (!await context.Producers.AnyAsync(p => p.Id == producerId, cancellationToken).ConfigureAwait(false))
            throw new KeyNotFoundException($"No se encontró la productora con Id {producerId}.");

        var resolvedWebLinkId = await ResolveWebLinkIdForActionAsync(
                context,
                webLinkId,
                action,
                cancellationToken)
            .ConfigureAwait(false);

        var maxSortOrder = await context.ProducerLinks
            .Where(link => link.ProducerId == producerId)
            .MaxAsync(link => (int?)link.SortOrder, cancellationToken)
            .ConfigureAwait(false) ?? -1;

        var entity = new ProducerLink
        {
            ProducerId = producerId,
            WebLinkId = resolvedWebLinkId,
            Title = normalized.Title,
            Url = normalized.Url,
            Action = normalized.Action,
            Notes = normalized.Notes,
            SortOrder = maxSortOrder + 1,
            CreatedAt = DateTime.UtcNow
        };

        context.ProducerLinks.Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (await GetByIdAsync(entity.Id, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<ProducerLink> UpdateAsync(
        int id,
        int? webLinkId,
        string title,
        string url,
        ActressLinkAction action,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(title, url, action, notes);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.ProducerLinks
            .FirstOrDefaultAsync(link => link.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"No se encontró el enlace con Id {id}.");

        entity.WebLinkId = await ResolveWebLinkIdForActionAsync(
                context,
                webLinkId,
                action,
                cancellationToken)
            .ConfigureAwait(false);
        entity.Title = normalized.Title;
        entity.Url = normalized.Url;
        entity.Action = normalized.Action;
        entity.Notes = normalized.Notes;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (await GetByIdAsync(entity.Id, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.ProducerLinks
            .FirstOrDefaultAsync(link => link.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
            return;

        context.ProducerLinks.Remove(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> OpenInBrowserAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.ProducerLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(link => link.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is not null && _browserLauncher.TryOpenInFirefox(entity.Url);
    }

    public async Task MarkScrapedAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.ProducerLinks
            .FirstOrDefaultAsync(link => link.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
            return;

        entity.LastScrapedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int?> ResolveWebLinkIdForActionAsync(
        AppDbContext context,
        int? webLinkId,
        ActressLinkAction action,
        CancellationToken cancellationToken)
    {
        if (action == ActressLinkAction.Scrape)
        {
            if (webLinkId is null)
            {
                throw new ArgumentException(
                    "Los enlaces de scraping requieren un sitio del Link Manager (productora).",
                    nameof(webLinkId));
            }

            var webLink = await context.WebLinks
                .AsNoTracking()
                .FirstOrDefaultAsync(link => link.Id == webLinkId.Value, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"No se encontró el WebLink con Id {webLinkId.Value}.");

            if (string.IsNullOrWhiteSpace(webLink.ScrapeHintsJson))
            {
                throw new ArgumentException(
                    $"El sitio «{webLink.Nombre}» no tiene selectores de scraping. Configurelos una vez en Link Manager.",
                    nameof(webLinkId));
            }

            return webLinkId;
        }

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
        string? Notes) Normalize(
        string title,
        string url,
        ActressLinkAction action,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El título del enlace es obligatorio.", nameof(title));

        var normalizedUrl = NormalizeUrl(url);
        var trimmedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        return (title.Trim(), normalizedUrl, action, trimmedNotes);
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
