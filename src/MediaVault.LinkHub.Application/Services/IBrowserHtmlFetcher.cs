namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// Obtiene HTML de una URL usando un navegador real (p. ej. WebView2).
/// Útil cuando el sitio bloquea peticiones HttpClient (403 / Cloudflare / age-gate).
/// </summary>
public interface IBrowserHtmlFetcher
{
    /// <summary>
    /// Navega a la URL y devuelve el HTML del documento (tras carga / JS inicial).
    /// </summary>
    Task<string> FetchHtmlAsync(
        string url,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
