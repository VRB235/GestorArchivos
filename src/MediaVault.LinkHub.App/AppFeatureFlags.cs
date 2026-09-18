namespace MediaVault.LinkHub.App;

/// <summary>
/// Interruptores de producto. El scraping está activo; use Scraping Lab
/// para depurar selectores en sitios difíciles (SPA/WAF) antes de confiar
/// en el scrape productivo de Actrices/Productoras.
/// </summary>
public static class AppFeatureFlags
{
    /// <summary>
    /// Cuando es <c>false</c>, se ocultan editores de hints, botones de scrape,
    /// grids de videos scrapeados y se bloquean los comandos asociados.
    /// </summary>
    public static bool IsWebScrapingEnabled => true;
}
