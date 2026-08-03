using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// CRUD de categorías de video para Media Vault.
/// </summary>
public interface IVideoCategoryService
{
    Task<IReadOnlyList<VideoCategory>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<VideoCategory> CreateAsync(string name, CancellationToken cancellationToken = default);

    Task<VideoCategory> UpdateAsync(int id, string name, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
