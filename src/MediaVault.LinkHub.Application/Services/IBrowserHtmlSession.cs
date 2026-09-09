using MediaVault.LinkHub.Application.Models.Scraping;

namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// Sesión de navegador reutilizable (una ventana WebView2) para paginar tras age-gate/Cloudflare.
/// </summary>
public interface IBrowserHtmlSession : IAsyncDisposable
{
    Task<BrowserHtmlFetchResult> NavigateAsync(
        string url,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
