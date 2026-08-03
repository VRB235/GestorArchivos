using MediaVault.LinkHub.Application.Services;

using MediaVault.LinkHub.Domain.Entities;

using MediaVault.LinkHub.Domain.Enums;

using MediaVault.LinkHub.Infrastructure.Data;

using MediaVault.LinkHub.Infrastructure.Launchers;

using Microsoft.EntityFrameworkCore;



namespace MediaVault.LinkHub.Infrastructure.Services;



public sealed class WebLinkService : IWebLinkService

{

  private readonly IDbContextFactory<AppDbContext> _contextFactory;



  public WebLinkService(IDbContextFactory<AppDbContext> contextFactory)

  {

    _contextFactory = contextFactory;

  }



  public async Task<IReadOnlyList<WebLink>> GetAllAsync(CancellationToken cancellationToken = default)

  {

    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);



    return await context.WebLinks

      .AsNoTracking()

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

      .Where(link => link.Categoria == categoria)

      .OrderBy(link => link.FechaUltimaActualizacion ?? DateTime.MaxValue)

      .ThenBy(link => link.Nombre)

      .ToListAsync(cancellationToken)

      .ConfigureAwait(false);

  }



  public async Task<WebLink?> GetByIdAsync(int id, CancellationToken cancellationToken = default)

  {

    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    return await context.WebLinks.AsNoTracking().FirstOrDefaultAsync(link => link.Id == id, cancellationToken).ConfigureAwait(false);

  }



  public async Task<WebLink> CreateAsync(

    string nombre,

    string url,

    LinkCategory categoria,

    string? logoPath = null,

    DateTime? fechaUltimaActualizacionUsuario = null,

    CancellationToken cancellationToken = default)

  {

    ValidateLinkInput(nombre, url);



    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);



    var entity = new WebLink

    {

      Nombre = nombre.Trim(),

      Url = NormalizeUrl(url),

      Categoria = categoria,

      LogoPath = string.IsNullOrWhiteSpace(logoPath) ? null : logoPath.Trim(),

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

    CancellationToken cancellationToken = default)

  {

    ValidateLinkInput(nombre, url);



    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);



    var entity = await context.WebLinks.FirstOrDefaultAsync(link => link.Id == id, cancellationToken).ConfigureAwait(false)

      ?? throw new KeyNotFoundException($"No se encontró el enlace con Id {id}.");



    entity.Nombre = nombre.Trim();

    entity.Url = NormalizeUrl(url);

    entity.Categoria = categoria;

    entity.LogoPath = string.IsNullOrWhiteSpace(logoPath) ? null : logoPath.Trim();

    entity.FechaUltimaActualizacion = NormalizeUserVisitDate(fechaUltimaActualizacionUsuario);



    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    return entity;

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



    context.WebLinks.Remove(entity);

    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

  }



  public async Task<bool> OpenInBrowserAsync(int id, CancellationToken cancellationToken = default)

  {

    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);



    var entity = await context.WebLinks.AsNoTracking().FirstOrDefaultAsync(link => link.Id == id, cancellationToken).ConfigureAwait(false);

    if (entity is null)

      return false;



    return BrowserLauncher.TryOpenInPrivateWindow(entity.Url);

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


