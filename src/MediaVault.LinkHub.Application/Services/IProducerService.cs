using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// CRUD de productoras/fuentes para etiquetar videos en Media Vault.
/// </summary>
public interface IProducerService
{
    Task<IReadOnlyList<Producer>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Producer> CreateAsync(string name, CancellationToken cancellationToken = default);

    Task<Producer> UpdateAsync(int id, string name, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
