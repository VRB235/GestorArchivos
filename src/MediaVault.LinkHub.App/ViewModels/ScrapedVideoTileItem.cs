using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;

using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class ScrapedVideoTileItem : ObservableObject
{
    public required ScrapedVideo Video { get; init; }

    public string Title => Video.Title;

    public string? SourceUrl => Video.SourceUrl;

    public string? ThumbnailUrl => Video.ThumbnailUrl;

    public string? Code => Video.Code;

    public string? DurationText => Video.DurationText;

    public bool IsNew => Video.IsNew;

    public string BadgeText => IsNew ? "Nuevo" : "Visto";

    [ObservableProperty]
    private ImageSource? _thumbnail;
}
