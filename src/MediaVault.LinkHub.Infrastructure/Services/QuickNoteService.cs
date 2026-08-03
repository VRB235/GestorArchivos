using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MediaVault.LinkHub.Infrastructure.Services;

public sealed class QuickNoteService : IQuickNoteService
{
  private readonly IDbContextFactory<AppDbContext> _contextFactory;

  public QuickNoteService(IDbContextFactory<AppDbContext> contextFactory)
  {
    _contextFactory = contextFactory;
  }

  public async Task<IReadOnlyList<QuickNote>> GetAllAsync(CancellationToken cancellationToken = default)
  {
    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    return await context.QuickNotes
      .AsNoTracking()
      .OrderByDescending(note => note.FechaCreacion)
      .ToListAsync(cancellationToken)
      .ConfigureAwait(false);
  }

  public async Task<QuickNote?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
  {
    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    return await context.QuickNotes.AsNoTracking().FirstOrDefaultAsync(note => note.Id == id, cancellationToken).ConfigureAwait(false);
  }

  public async Task<QuickNote> CreateAsync(string contenido, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(contenido))
      throw new ArgumentException("El contenido de la nota es obligatorio.", nameof(contenido));

    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    var entity = new QuickNote
    {
      Contenido = contenido.Trim(),
      FechaCreacion = DateTime.UtcNow
    };

    context.QuickNotes.Add(entity);
    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    return entity;
  }

  public async Task<QuickNote> UpdateAsync(int id, string contenido, CancellationToken cancellationToken = default)
  {
    if (string.IsNullOrWhiteSpace(contenido))
      throw new ArgumentException("El contenido de la nota es obligatorio.", nameof(contenido));

    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    var entity = await context.QuickNotes.FirstOrDefaultAsync(note => note.Id == id, cancellationToken).ConfigureAwait(false)
      ?? throw new KeyNotFoundException($"No se encontró la nota con Id {id}.");

    entity.Contenido = contenido.Trim();
    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    return entity;
  }

  public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
  {
    await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

    var entity = await context.QuickNotes.FirstOrDefaultAsync(note => note.Id == id, cancellationToken).ConfigureAwait(false);
    if (entity is null)
      return;

    context.QuickNotes.Remove(entity);
    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
  }
}
