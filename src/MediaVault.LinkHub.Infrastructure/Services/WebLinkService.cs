using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Domain.Enums;
using MediaVault.LinkHub.Domain.Media;
using MediaVault.LinkHub.Infrastructure.Data;
using MediaVault.LinkHub.Infrastructure.Launchers;
using MediaVault.LinkHub.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;

namespace MediaVault.LinkHub.Infrastructure.Services;

public sealed class WebLinkService : IWebLinkService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly LinkLogoStorage _logoStorage;

    public WebLinkService(IDbContextFactory<AppDbContext> contextFactory)
        : this(contextFactory, new LinkLogoStorage())
    {
    }

    public WebLinkService(IDbContextFactory<AppDbContext> contextFactory, LinkLogoStorage logoStorage)
    {
        _contextFactory = contextFactory;
        _logoStorage = logoStorage;
    }

    public async Task<IReadOnlyList<WebLink>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.WebLinks
            .AsNoTracking()
            .Include(link => link.Producers)
            .OrderBy(link => link.FechaUltimaActualizacion ?? DateTime.MaxValue)
            .ThenBy(link => link.Nombre)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WebLink>> GetByCategoryAsync(
        LinkCategory categoria,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.WebLinks
            .AsNoTracking()
            .Include(link => link.Producers)
            .Where(link => link.Categoria == categoria)
            .OrderBy(link => link.FechaUltimaActualizacion ?? DateTime.MaxValue)
            .ThenBy(link => link.Nombre)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WebLink?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.WebLinks
            .AsNoTracking()
            .Include(link => link.Producers)
            .FirstOrDefaultAsync(link => link.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WebLink> CreateAsync(
        string nombre,
        string url,
        LinkCategory categoria,
        string? logoPath = null,
        DateTime? fechaUltimaActualizacionUsuario = null,
        double logoZoom = 1.0,
        double logoOffsetX = 0.0,
        double logoOffsetY = 0.0,
        CancellationToken cancellationToken = default)
    {
        ValidateLinkInput(nombre, url);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = new WebLink
        {
            Nombre = nombre.Trim(),
            Url = NormalizeUrl(url),
            Categoria = categoria,
            LogoPath = ResolvePersistedLogoPath(logoPath),
            LogoZoom = WebLinkLogoFit.ClampZoom(logoZoom),
            LogoOffsetX = WebLinkLogoFit.ClampOffset(logoOffsetX),
            LogoOffsetY = WebLinkLogoFit.ClampOffset(logoOffsetY),
            FechaCreacion = DateTime.UtcNow,
            FechaUltimaActualizacion = NormalizeUserVisitDate(fechaUltimaActualizacionUsuario)
        };

        context.WebLinks.Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity;
    }

    public async Task<WebLink> UpdateAsync(
        int id,
        string nombre,
        string url,
        LinkCategory categoria,
        string? logoPath = null,
        DateTime? fechaUltimaActualizacionUsuario = null,
        double logoZoom = 1.0,
        double logoOffsetX = 0.0,
        double logoOffsetY = 0.0,
        CancellationToken cancellationToken = default)
    {
        ValidateLinkInput(nombre, url);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.WebLinks.FirstOrDefaultAsync(link => link.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"No se encontró el enlace con Id {id}.");

        var previousLogoPath = entity.LogoPath;
        var nextLogoPath = ResolvePersistedLogoPath(logoPath);

        entity.Nombre = nombre.Trim();
        entity.Url = NormalizeUrl(url);
        entity.Categoria = categoria;
        entity.LogoPath = nextLogoPath;
        entity.LogoZoom = WebLinkLogoFit.ClampZoom(logoZoom);
        entity.LogoOffsetX = WebLinkLogoFit.ClampOffset(logoOffsetX);
        entity.LogoOffsetY = WebLinkLogoFit.ClampOffset(logoOffsetY);
        entity.FechaUltimaActualizacion = NormalizeUserVisitDate(fechaUltimaActualizacionUsuario);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (!PathsEqual(previousLogoPath, nextLogoPath))
            _logoStorage.TryDeleteManaged(previousLogoPath);

        return entity;
    }

    public async Task<WebLink> UpdateProducersAsync(
        int id,
        IReadOnlyCollection<int> producerIds,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.WebLinks
            .Include(link => link.Producers)
            .FirstOrDefaultAsync(link => link.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"No se encontró el enlace con Id {id}.");

        var distinctIds = producerIds.Distinct().ToList();

        if (distinctIds.Count > 0)
        {
            var existingIds = await context.Producers
                .Where(producer => distinctIds.Contains(producer.Id))
                .Select(producer => producer.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (existingIds.Count != distinctIds.Count)
                throw new KeyNotFoundException("Una o más productoras seleccionadas no existen.");
        }

        entity.Producers.Clear();

        if (distinctIds.Count > 0)
        {
            var producers = await context.Producers
                .Where(producer => distinctIds.Contains(producer.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var producer in producers)
                entity.Producers.Add(producer);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await context.WebLinks
            .AsNoTracking()
            .Include(link => link.Producers)
            .FirstAsync(link => link.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<WebLink> MarkAsUserUpdatedAsync(
        int id,
        DateTime? fechaVisita = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.WebLinks.FirstOrDefaultAsync(link => link.Id == id, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"No se encontró el enlace con Id {id}.");

        entity.FechaUltimaActualizacion = NormalizeUserVisitDate(fechaVisita ?? DateTime.UtcNow);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.WebLinks.FirstOrDefaultAsync(link => link.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return;

        var logoPath = entity.LogoPath;
        context.WebLinks.Remove(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logoStorage.TryDeleteManaged(logoPath);
    }

    public async Task<bool> OpenInBrowserAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.WebLinks.AsNoTracking().FirstOrDefaultAsync(link => link.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            return false;

        return BrowserLauncher.TryOpenInFirefox(entity.Url);
    }

    /// <summary>
    /// Copia a almacenamiento managed los logos que aún apuntan a archivos externos existentes.
    /// </summary>
    public async Task MigrateExternalLogosAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var links = await context.WebLinks
            .Where(link => link.LogoPath != null && link.LogoPath != string.Empty)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var changed = false;
        foreach (var link in links)
        {
            if (_logoStorage.IsManagedPath(link.LogoPath))
                continue;

            if (string.IsNullOrWhiteSpace(link.LogoPath) || !File.Exists(link.LogoPath))
                continue;

            try
            {
                link.LogoPath = _logoStorage.Persist(link.LogoPath);
                changed = true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Conservar ruta externa si la copia falla; el usuario podrá reasignar el logo.
            }
        }

        if (changed)
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private string? ResolvePersistedLogoPath(string? logoPath)
    {
        if (string.IsNullOrWhiteSpace(logoPath))
            return null;

        return _logoStorage.Persist(logoPath.Trim());
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
            return true;

        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void ValidateLinkInput(string nombre, string url)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del enlace es obligatorio.", nameof(nombre));

        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("La URL del enlace es obligatoria.", nameof(url));
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

    private static DateTime? NormalizeUserVisitDate(DateTime? fecha)
    {
        if (!fecha.HasValue)
            return null;

        return fecha.Value.Kind switch
        {
            DateTimeKind.Utc => fecha.Value,
            DateTimeKind.Local => fecha.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(fecha.Value, DateTimeKind.Local).ToUniversalTime()
        };
    }
}
