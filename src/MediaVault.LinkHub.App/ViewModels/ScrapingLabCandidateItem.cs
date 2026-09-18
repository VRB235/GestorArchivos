using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;

using MediaVault.LinkHub.Application.Models.Scraping;

namespace MediaVault.LinkHub.App.ViewModels;

/// <summary>
/// Ítem de resultado dry-run del Scraping Lab (no persistido).
/// </summary>
public partial class ScrapingLabCandidateItem : ObservableObject
{
    public required ScrapedVideoCandidate Candidate { get; init; }

    public string Title => Candidate.Title;

    public string SourceUrl => Candidate.SourceUrl;

    public string? Code => Candidate.Code;

    public string? DurationText => Candidate.DurationText;

    public DateTime? PublishedAt => Candidate.PublishedAt;

    public string? ThumbnailUrl => Candidate.ThumbnailUrl;

    public string? PreviewUrl => Candidate.PreviewUrl;

    public bool HasPreviewUrl => !string.IsNullOrWhiteSpace(PreviewUrl);

    public bool PreviewIsVideo =>
        !string.IsNullOrWhiteSpace(PreviewUrl) && IsVideoUrl(PreviewUrl);

    public bool PreviewIsBlob =>
        !string.IsNullOrWhiteSpace(PreviewUrl)
        && PreviewUrl.StartsWith("blob:", StringComparison.OrdinalIgnoreCase);

    public bool PreviewIsImage =>
        HasPreviewUrl
        && !PreviewIsVideo
        && !PreviewIsBlob
        && IsImageOrAnimatedUrl(PreviewUrl!);

    [ObservableProperty]
    private ImageSource? _thumbnail;

    public void NotifyPreviewChanged()
    {
        OnPropertyChanged(nameof(PreviewUrl));
        OnPropertyChanged(nameof(HasPreviewUrl));
        OnPropertyChanged(nameof(PreviewIsVideo));
        OnPropertyChanged(nameof(PreviewIsBlob));
        OnPropertyChanged(nameof(PreviewIsImage));
    }

    private static bool IsVideoUrl(string url)
    {
        var lower = url.ToLowerInvariant();
        return lower.Contains(".mp4", StringComparison.Ordinal)
            || lower.Contains(".webm", StringComparison.Ordinal)
            || lower.Contains(".m3u8", StringComparison.Ordinal);
    }

    private static bool IsImageOrAnimatedUrl(string url)
    {
        var lower = url.ToLowerInvariant();
        return lower.Contains(".gif", StringComparison.Ordinal)
            || lower.Contains(".webp", StringComparison.Ordinal)
            || lower.Contains(".jpg", StringComparison.Ordinal)
            || lower.Contains(".jpeg", StringComparison.Ordinal)
            || lower.Contains(".png", StringComparison.Ordinal);
    }
}
