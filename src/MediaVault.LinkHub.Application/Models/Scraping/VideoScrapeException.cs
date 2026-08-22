namespace MediaVault.LinkHub.Application.Models.Scraping;

/// <summary>
/// Error de scrape que conserva el log de diagnóstico acumulado.
/// </summary>
public sealed class VideoScrapeException : Exception
{
    public VideoScrapeException(string message, IReadOnlyList<string> diagnosticLog, Exception? inner = null)
        : base(message, inner)
    {
        DiagnosticLog = diagnosticLog;
    }

    public IReadOnlyList<string> DiagnosticLog { get; }
}
