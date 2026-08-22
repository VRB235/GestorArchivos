namespace MediaVault.LinkHub.Application.Models.Scraping;

/// <summary>
/// Resultado agregado de una ejecución de scraping.
/// </summary>
public sealed class VideoScrapeResult
{
    public int ActressLinkId { get; init; }

    public int ActressId { get; init; }

    public string SourceUrl { get; init; } = string.Empty;

    public IReadOnlyList<ScrapedVideoCandidate> Items { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Log técnico de la ejecución (selectores, HTTP, coincidencias, omisiones).
    /// </summary>
    public IReadOnlyList<string> DiagnosticLog { get; init; } = [];

    public int PagesFetched { get; init; }
}
