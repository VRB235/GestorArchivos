using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// Contrato del módulo Scratchpad: CRUD de notas rápidas.
/// </summary>
public interface IQuickNoteService
{
    Task<IReadOnlyList<QuickNote>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<QuickNote?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<QuickNote> CreateAsync(string contenido, CancellationToken cancellationToken = default);

    Task<QuickNote> UpdateAsync(int id, string contenido, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
