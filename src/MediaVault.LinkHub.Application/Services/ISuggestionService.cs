using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Domain.Enums;

namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// Contrato del módulo Sugerencias: texto, imágenes, fecha y estado resuelto.
/// </summary>
public interface ISuggestionService
{
    Task<IReadOnlyList<Suggestion>> GetAllAsync(
        bool? soloResueltos = null,
        CancellationToken cancellationToken = default);

    Task<Suggestion?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Suggestion> CreateAsync(
        string texto,
        SuggestionKind tipo,
        IReadOnlyCollection<string>? imageSourcePaths = null,
        CancellationToken cancellationToken = default);

    Task<Suggestion> UpdateAsync(
        int id,
        string texto,
        SuggestionKind tipo,
        CancellationToken cancellationToken = default);

    Task<Suggestion> SetResolvedAsync(
        int id,
        bool resuelto,
        CancellationToken cancellationToken = default);

    Task<SuggestionAttachment> AddAttachmentAsync(
        int suggestionId,
        string imageSourcePath,
        CancellationToken cancellationToken = default);

    Task RemoveAttachmentAsync(int attachmentId, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
