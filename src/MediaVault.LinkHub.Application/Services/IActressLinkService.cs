using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Domain.Enums;

namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// CRUD de enlaces asociados a actrices (navegación o fuentes de scrape).
/// </summary>
public interface IActressLinkService
{
    Task<IReadOnlyList<ActressLink>> GetByActressIdAsync(int actressId, CancellationToken cancellationToken = default);

    Task<ActressLink?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<ActressLink> CreateAsync(
        int actressId,
        int? webLinkId,
        string title,
        string url,
        ActressLinkAction action,
        string? notes,
        string? scrapeHintsJson,
        string? scraperKey,
        CancellationToken cancellationToken = default);

    Task<ActressLink> UpdateAsync(
        int id,
        int? webLinkId,
        string title,
        string url,
        ActressLinkAction action,
        string? notes,
        string? scrapeHintsJson,
        string? scraperKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> OpenInBrowserAsync(int id, CancellationToken cancellationToken = default);

    Task MarkScrapedAsync(int id, CancellationToken cancellationToken = default);
}
