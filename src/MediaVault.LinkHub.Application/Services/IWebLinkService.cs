using MediaVault.LinkHub.Domain.Entities;

using MediaVault.LinkHub.Domain.Enums;



namespace MediaVault.LinkHub.Application.Services;



/// <summary>

/// Contrato del módulo Link Manager: CRUD de enlaces y apertura en navegador.

/// </summary>

public interface IWebLinkService

{

    Task<IReadOnlyList<WebLink>> GetAllAsync(CancellationToken cancellationToken = default);



    Task<IReadOnlyList<WebLink>> GetByCategoryAsync(

        LinkCategory categoria,

        CancellationToken cancellationToken = default);



    Task<WebLink?> GetByIdAsync(int id, CancellationToken cancellationToken = default);



    Task<WebLink> CreateAsync(

        string nombre,

        string url,

        LinkCategory categoria,

        string? logoPath = null,

        DateTime? fechaUltimaActualizacionUsuario = null,

        CancellationToken cancellationToken = default);



    Task<WebLink> UpdateAsync(

        int id,

        string nombre,

        string url,

        LinkCategory categoria,

        string? logoPath = null,

        DateTime? fechaUltimaActualizacionUsuario = null,

        CancellationToken cancellationToken = default);



    Task DeleteAsync(int id, CancellationToken cancellationToken = default);



    /// <summary>

    /// Marca manualmente la fecha de última visita/revisión del sitio por el usuario.

    /// </summary>

    Task<WebLink> MarkAsUserUpdatedAsync(

        int id,

        DateTime? fechaVisita = null,

        CancellationToken cancellationToken = default);



    /// <summary>

    /// Abre la URL en el navegador predeterminado en modo incógnito/privado.

    /// </summary>

    Task<bool> OpenInBrowserAsync(int id, CancellationToken cancellationToken = default);

}


