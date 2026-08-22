using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;

using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class ScrapedVideoListItem : ObservableObject
{
    public required ScrapedVideo Video { get; init; }

    public string Title => Video.Title;

    public string SourceUrl => Video.SourceUrl;

    public string? Code => Video.Code;

    public string? DurationText => Video.DurationText;

    public DateTime? PublishedAt => Video.PublishedAt;

    public string? ThumbnailUrl => Video.ThumbnailUrl;

    public string? PreviewUrl => Video.PreviewUrl;

    public bool HasAnimatedPreview =>
        !string.IsNullOrWhiteSpace(PreviewUrl) && IsVideoOrAnimatedUrl(PreviewUrl);

    public bool PreviewIsVideo =>
        !string.IsNullOrWhiteSpace(PreviewUrl) && IsVideoUrl(PreviewUrl);

    [ObservableProperty]
    private ImageSource? _thumbnail;

    private static bool IsVideoUrl(string url)
    {
        var lower = url.ToLowerInvariant();
        return lower.Contains(".mp4", StringComparison.Ordinal)
            || lower.Contains(".webm", StringComparison.Ordinal)
            || lower.Contains(".m3u8", StringComparison.Ordinal);
    }

    private static bool IsVideoOrAnimatedUrl(string url)
    {
        var lower = url.ToLowerInvariant();
        return IsVideoUrl(url)
            || lower.Contains(".gif", StringComparison.Ordinal)
            || lower.Contains(".webp", StringComparison.Ordinal);
    }
}
