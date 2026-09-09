namespace MediaVault.LinkHub.App;

/// <summary>
/// Interruptores de producto. El scraping web está desactivado en UI mientras
/// el enfoque CSS+WebView2 siga siendo inestable en sitios SPA/WAF.
/// </summary>
public static class AppFeatureFlags
{
    /// <summary>
    /// Cuando es <c>false</c>, se ocultan editores de hints, botones de scrape,
    /// grids de videos scrapeados y se bloquean los comandos asociados.
    /// El código de infraestructura permanece para reactivarlo.
    /// </summary>
    public static bool IsWebScrapingEnabled => false;
}
