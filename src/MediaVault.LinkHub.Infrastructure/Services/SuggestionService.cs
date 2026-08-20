using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Domain.Enums;
using MediaVault.LinkHub.Infrastructure.Data;
using MediaVault.LinkHub.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;

namespace MediaVault.LinkHub.Infrastructure.Services;

public sealed class SuggestionService : ISuggestionService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly SuggestionImageStorage _imageStorage;

    public SuggestionService(
        IDbContextFactory<AppDbContext> contextFactory,
        SuggestionImageStorage imageStorage)
    {
        _contextFactory = contextFactory;
        _imageStorage = imageStorage;
    }

    public async Task<IReadOnlyList<Suggestion>> GetAllAsync(
        bool? soloResueltos = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var query = context.Suggestions
            .AsNoTracking()
            .Include(suggestion => suggestion.Attachments)
            .AsQueryable();

        if (soloResueltos.HasValue)
            query = query.Where(suggestion => suggestion.Resuelto == soloResueltos.Value);

        return await query
            .OrderBy(suggestion => suggestion.Resuelto)
            .ThenByDescending(suggestion => suggestion.FechaCreacion)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Suggestion?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.Suggestions
            .AsNoTracking()
            .Include(suggestion => suggestion.Attachments)
            .FirstOrDefaultAsync(suggestion => suggestion.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Suggestion> CreateAsync(
        string texto,
        SuggestionKind tipo,
        IReadOnlyCollection<string>? imageSourcePaths = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeTexto(texto);
        var persistedPaths = new List<(string Path, string OriginalName)>();

        try
        {
            foreach (var source in imageSourcePaths ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(source))
                    continue;

                var managed = _imageStorage.Persist(source);
                persistedPaths.Add((managed, Path.GetFileName(source)));
            }

            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var entity = new Suggestion
            {
                Texto = normalized,
                Tipo = tipo,
                FechaCreacion = DateTime.UtcNow,
                Resuelto = false
            };

            foreach (var (path, originalName) in persistedPaths)
            {
                entity.Attachments.Add(new SuggestionAttachment
                {
                    FilePath = path,
                    OriginalFileName = originalName,
                    FechaCreacion = DateTime.UtcNow
                });
            }

            context.Suggestions.Add(entity);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return (await GetByIdAsync(entity.Id, cancellationToken).ConfigureAwait(false))!;
        }
        catch
        {
            foreach (var (path, _) in persistedPaths)
                _imageStorage.TryDeleteManaged(path);
            throw;
        }
    }

    public async Task<Suggestion> UpdateAsync(
        int id,
        string texto,
        SuggestionKind tipo,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeTexto(texto);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.Suggestions
            .FirstOrDefaultAsync(suggestion => suggestion.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"No se encontró la sugerencia con Id {id}.");

        entity.Texto = normalized;
        entity.Tipo = tipo;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (await GetByIdAsync(id, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<Suggestion> SetResolvedAsync(
        int id,
        bool resuelto,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.Suggestions
            .FirstOrDefaultAsync(suggestion => suggestion.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"No se encontró la sugerencia con Id {id}.");

        entity.Resuelto = resuelto;
        entity.FechaResuelto = resuelto ? DateTime.UtcNow : null;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (await GetByIdAsync(id, cancellationToken).ConfigureAwait(false))!;
    }

    public async Task<SuggestionAttachment> AddAttachmentAsync(
        int suggestionId,
        string imageSourcePath,
        CancellationToken cancellationToken = default)
    {
        var managedPath = _imageStorage.Persist(imageSourcePath);

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            var exists = await context.Suggestions
                .AnyAsync(suggestion => suggestion.Id == suggestionId, cancellationToken)
                .ConfigureAwait(false);
            if (!exists)
                throw new KeyNotFoundException($"No se encontró la sugerencia con Id {suggestionId}.");

            var attachment = new SuggestionAttachment
            {
                SuggestionId = suggestionId,
                FilePath = managedPath,
                OriginalFileName = Path.GetFileName(imageSourcePath),
                FechaCreacion = DateTime.UtcNow
            };

            context.SuggestionAttachments.Add(attachment);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return attachment;
        }
        catch
        {
            _imageStorage.TryDeleteManaged(managedPath);
            throw;
        }
    }

    public async Task RemoveAttachmentAsync(int attachmentId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var attachment = await context.SuggestionAttachments
            .FirstOrDefaultAsync(item => item.Id == attachmentId, cancellationToken)
            .ConfigureAwait(false);
        if (attachment is null)
            return;

        var path = attachment.FilePath;
        context.SuggestionAttachments.Remove(attachment);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _imageStorage.TryDeleteManaged(path);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.Suggestions
            .Include(suggestion => suggestion.Attachments)
            .FirstOrDefaultAsync(suggestion => suggestion.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
            return;

        var paths = entity.Attachments.Select(attachment => attachment.FilePath).ToList();
        context.Suggestions.Remove(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var path in paths)
            _imageStorage.TryDeleteManaged(path);
    }

    private static string NormalizeTexto(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            throw new ArgumentException("El texto de la sugerencia es obligatorio.", nameof(texto));

        return texto.Trim();
    }
}
