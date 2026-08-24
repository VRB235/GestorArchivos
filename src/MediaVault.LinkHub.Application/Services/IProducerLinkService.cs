using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Domain.Enums;

namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// CRUD de enlaces asociados a productoras (navegación o fuentes de scrape de catálogo).
/// </summary>
public interface IProducerLinkService
{
    Task<IReadOnlyList<ProducerLink>> GetByProducerIdAsync(int producerId, CancellationToken cancellationToken = default);

    Task<ProducerLink?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ProducerLink> CreateAsync(
        int producerId,
        int? webLinkId,
        string title,
        string url,
        ActressLinkAction action,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<ProducerLink> UpdateAsync(
        int id,
        int? webLinkId,
        string title,
        string url,
        ActressLinkAction action,
        string? notes,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> OpenInBrowserAsync(int id, CancellationToken cancellationToken = default);

    Task MarkScrapedAsync(int id, CancellationToken cancellationToken = default);
}
