using MediaVault.LinkHub.Application.Models.Scraping;

namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// Obtiene HTML de una URL usando un navegador real (p. ej. WebView2).
/// Útil cuando el sitio bloquea peticiones HttpClient (403 / Cloudflare / age-gate).
/// </summary>
public interface IBrowserHtmlFetcher
{
    /// <summary>
    /// Navega a la URL (ventana temporal), espera JS/age-gate y devuelve HTML + cookies.
    /// </summary>
    Task<BrowserHtmlFetchResult> FetchAsync(
        string url,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compatibilidad: solo HTML.
    /// </summary>
    Task<string> FetchHtmlAsync(
        string url,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Abre una sesión de navegador reutilizable (misma ventana / mismo perfil) para varias URLs.
    /// </summary>
    Task<IBrowserHtmlSession> BeginSessionAsync(CancellationToken cancellationToken = default);
}
