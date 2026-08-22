namespace MediaVault.LinkHub.Application.Models.Scraping;

/// <summary>
/// Resultado intermedio de un scrape antes de persistir.
/// </summary>
public sealed class ScrapedVideoCandidate
{
    public string Title { get; set; } = string.Empty;

    public string SourceUrl { get; set; } = string.Empty;

    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Preview al hover (mp4/gif/webp) cuando el HTML lo incluye.
    /// </summary>
    public string? PreviewUrl { get; set; }

    public string? Code { get; set; }

    public string? DurationText { get; set; }

    public DateTime? PublishedAt { get; set; }

    public Dictionary<string, string>? Extra { get; set; }
}
