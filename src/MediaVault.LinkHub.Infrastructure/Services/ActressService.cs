using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MediaVault.LinkHub.Infrastructure.Services;

public sealed class ActressService : IActressService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public ActressService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Actress>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var actresses = await context.Actresses
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return actresses
            .OrderBy(actress => actress.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<Actress> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeName(name);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        if (await context.Actresses.AnyAsync(
                actress => actress.Name == normalizedName,
                cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Ya existe la actriz «{normalizedName}».");
        }

        var maxSortOrder = await context.Actresses
            .MaxAsync(actress => (int?)actress.SortOrder, cancellationToken)
            .ConfigureAwait(false) ?? -1;

        var entity = new Actress
        {
            Name = normalizedName,
            SortOrder = maxSortOrder + 1
        };

        context.Actresses.Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity;
    }

    public async Task<Actress> UpdateAsync(int id, string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeName(name);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.Actresses
            .FirstOrDefaultAsync(actress => actress.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"No se encontró la actriz con Id {id}.");

        if (await context.Actresses.AnyAsync(
                actress => actress.Id != id && actress.Name == normalizedName,
                cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Ya existe la actriz «{normalizedName}».");
        }

        entity.Name = normalizedName;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.Actresses
            .FirstOrDefaultAsync(actress => actress.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
            return;

        context.Actresses.Remove(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la actriz es obligatorio.", nameof(name));

        return name.Trim();
    }
}
