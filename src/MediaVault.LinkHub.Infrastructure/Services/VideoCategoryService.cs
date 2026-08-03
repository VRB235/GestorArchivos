using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MediaVault.LinkHub.Infrastructure.Services;

public sealed class VideoCategoryService : IVideoCategoryService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public VideoCategoryService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<VideoCategory>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.VideoCategories
            .AsNoTracking()
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<VideoCategory> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeName(name);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        if (await context.VideoCategories.AnyAsync(
                category => category.Name == normalizedName,
                cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Ya existe la categoría «{normalizedName}».");
        }

        var maxSortOrder = await context.VideoCategories
            .MaxAsync(category => (int?)category.SortOrder, cancellationToken)
            .ConfigureAwait(false) ?? -1;

        var entity = new VideoCategory
        {
            Name = normalizedName,
            SortOrder = maxSortOrder + 1
        };

        context.VideoCategories.Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity;
    }

    public async Task<VideoCategory> UpdateAsync(int id, string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeName(name);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.VideoCategories
            .FirstOrDefaultAsync(category => category.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"No se encontró la categoría con Id {id}.");

        if (await context.VideoCategories.AnyAsync(
                category => category.Id != id && category.Name == normalizedName,
                cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Ya existe la categoría «{normalizedName}».");
        }

        entity.Name = normalizedName;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.VideoCategories
            .FirstOrDefaultAsync(category => category.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
            return;

        context.VideoCategories.Remove(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la categoría es obligatorio.", nameof(name));

        return name.Trim();
    }
}
