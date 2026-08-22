namespace MediaVault.LinkHub.Domain.Enums;

/// <summary>
/// Comportamiento al activar un enlace asociado a una actriz.
/// </summary>
public enum ActressLinkAction
{
    /// <summary>
    /// Abre la URL en el navegador externo.
    /// </summary>
    Browse = 0,

    /// <summary>
    /// Abre la ventana de scraping in-app y extrae metadatos de videos.
    /// </summary>
    Scrape = 1
}
