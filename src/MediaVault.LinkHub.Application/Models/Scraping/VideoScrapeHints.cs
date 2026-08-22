namespace MediaVault.LinkHub.Application.Models.Scraping;

/// <summary>
/// Hints genéricos para scrapear listados/fichas de video mediante selectores CSS.
/// Se persisten como JSON en <c>ActressLink.ScrapeHintsJson</c>.
/// </summary>
public sealed class VideoScrapeHints
{
    /// <summary>
    /// Contenedor de cada ítem de video en el listado (obligatorio para listados).
    /// </summary>
    public string? ListItemSelector { get; set; }

    /// <summary>
    /// Selector del título relativo al ítem (o absoluto si no hay ListItemSelector).
    /// </summary>
    public string? TitleSelector { get; set; }

    /// <summary>
    /// Selector del enlace del video relativo al ítem.
    /// </summary>
    public string? UrlSelector { get; set; }

    /// <summary>
    /// Atributo del que se lee la URL (por defecto <c>href</c>).
    /// </summary>
    public string UrlAttribute { get; set; } = "href";

    public string? ThumbnailSelector { get; set; }

    public string ThumbnailAttribute { get; set; } = "src";

    /// <summary>
    /// Elemento (relativo al ítem) que contiene la URL del preview al hover.
    /// Si es null, se buscan atributos típicos (data-preview, data-trailer, etc.) en el ítem.
    /// </summary>
    public string? PreviewSelector { get; set; }

    /// <summary>
    /// Atributo del preview (p. ej. <c>data-preview</c>, <c>src</c>, <c>href</c>).
    /// Si es null con PreviewSelector, se prueban atributos habituales.
    /// </summary>
    public string? PreviewAttribute { get; set; }

    /// <summary>
    /// Selector del elemento sobre el que simular hover en captura con navegador (WebView2).
    /// Relativo al ítem. Por defecto se usa <c>img</c> / <c>picture</c> / el propio ítem.
    /// </summary>
    public string? PreviewHoverSelector { get; set; }

    /// <summary>
    /// Espera tras el hover simulado antes de leer el <c>video</c>/<c>src</c> (ms). Default 900.
    /// </summary>
    public int PreviewHoverWaitMs { get; set; } = 900;

    public string? CodeSelector { get; set; }

    public string? DateSelector { get; set; }

    /// <summary>
    /// Formato opcional para parsear fechas (p. ej. <c>yyyy-MM-dd</c>).
    /// </summary>
    public string? DateFormat { get; set; }

    public string? DurationSelector { get; set; }

    /// <summary>
    /// Selector del enlace/botón «siguiente página». Opcional: si no existe en el HTML,
    /// el scrape termina con lo ya extraído (no es error). Útil dejarlo preconfigurado
    /// para cuando el sitio habilite paginación en el futuro.
    /// </summary>
    public string? NextPageSelector { get; set; }

    /// <summary>
    /// Máximo de páginas a recorrer si <see cref="NextPageSelector"/> encuentra enlaces.
    /// Si no hay botón siguiente, solo se procesa la página inicial.
    /// </summary>
    public int MaxPages { get; set; } = 1;

    /// <summary>
    /// Espera opcional antes de parsear (ms). Útil si el HTML se sirve con delay; el scraper HTTP lo ignora.
    /// </summary>
    public int? WaitMs { get; set; }

    /// <summary>
    /// User-Agent override. Si es null se usa el del cliente HTTP por defecto.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Cabeceras HTTP adicionales (p. ej. Accept-Language).
    /// </summary>
    public Dictionary<string, string>? ExtraHeaders { get; set; }

    /// <summary>
    /// Si es true, URLs relativas se resuelven contra la URL del enlace; si hay BaseUrl, contra esa.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Plantilla de ejemplo mínima para listados CSS.
    /// </summary>
    public static VideoScrapeHints CreateListTemplate() =>
        new()
        {
            ListItemSelector = "div.video-item",
            TitleSelector = "a.title",
            UrlSelector = "a.title",
            UrlAttribute = "href",
            ThumbnailSelector = "img",
            ThumbnailAttribute = "src",
            PreviewSelector = null,
            PreviewAttribute = "data-preview",
            CodeSelector = ".code",
            DateSelector = ".date",
            DurationSelector = ".duration",
            NextPageSelector = "a.next",
            MaxPages = 3
        };
}
