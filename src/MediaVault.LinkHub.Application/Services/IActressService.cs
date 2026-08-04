using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// CRUD de actrices para etiquetar videos en Media Vault.
/// </summary>
public interface IActressService
{
    Task<IReadOnlyList<Actress>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Actress> CreateAsync(string name, CancellationToken cancellationToken = default);

    Task<Actress> UpdateAsync(int id, string name, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
