namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// Captura genérica de previews al hover simulando el navegador (p. ej. WebView2).
/// Sirve cuando el trailer no está en el HTML estático del listado.
/// </summary>
public interface IHoverPreviewCaptureService
{
    /// <summary>
    /// Abre la URL del listado, simula hover en cada ítem y devuelve SourceUrl → PreviewUrl.
    /// </summary>
    /// <param name="listUrl">URL del listado (ActressLink.Url).</param>
    /// <param name="listItemSelector">CSS de cada tarjeta (hints.ListItemSelector).</param>
    /// <param name="hoverSelector">CSS relativo al ítem para el hover; null = auto.</param>
    /// <param name="waitMs">Espera tras hover antes de leer el media.</param>
    /// <param name="progress">Mensajes de progreso (UI).</param>
    Task<IReadOnlyDictionary<string, string>> CaptureAsync(
        string listUrl,
        string listItemSelector,
        string? hoverSelector,
        int waitMs,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
