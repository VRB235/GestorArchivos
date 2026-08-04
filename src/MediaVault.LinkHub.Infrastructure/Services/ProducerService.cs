using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MediaVault.LinkHub.Infrastructure.Services;

public sealed class ProducerService : IProducerService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public ProducerService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<Producer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.Producers
            .AsNoTracking()
            .OrderBy(producer => producer.SortOrder)
            .ThenBy(producer => producer.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Producer> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeName(name);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        if (await context.Producers.AnyAsync(
                producer => producer.Name == normalizedName,
                cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Ya existe la productora «{normalizedName}».");
        }

        var maxSortOrder = await context.Producers
            .MaxAsync(producer => (int?)producer.SortOrder, cancellationToken)
            .ConfigureAwait(false) ?? -1;

        var entity = new Producer
        {
            Name = normalizedName,
            SortOrder = maxSortOrder + 1
        };

        context.Producers.Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity;
    }

    public async Task<Producer> UpdateAsync(int id, string name, CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeName(name);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.Producers
            .FirstOrDefaultAsync(producer => producer.Id == id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"No se encontró la productora con Id {id}.");

        if (await context.Producers.AnyAsync(
                producer => producer.Id != id && producer.Name == normalizedName,
                cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Ya existe la productora «{normalizedName}».");
        }

        entity.Name = normalizedName;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.Producers
            .FirstOrDefaultAsync(producer => producer.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
            return;

        context.Producers.Remove(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la productora es obligatorio.", nameof(name));

        return name.Trim();
    }
}
