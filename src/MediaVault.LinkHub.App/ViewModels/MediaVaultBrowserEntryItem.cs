using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;

using MediaVault.LinkHub.Application.Models.MediaVault;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Infrastructure.Media;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class MediaVaultBrowserEntryItem : ObservableObject
{
    public MediaVaultBrowserEntryItem(MediaVaultBrowserEntry entry) =>
        Entry = entry;

    public MediaVaultBrowserEntry Entry { get; }

    [ObservableProperty]
    private ImageSource? _thumbnail;

    [ObservableProperty]
    private bool _isThumbnailLoading;

    public string Name => Entry.Name;

    public string FileType => Entry.FileType;

    public string FullPath => Entry.FullPath;

    public bool IsDirectory => Entry.IsDirectory;

    public MediaFile? MediaFile => Entry.MediaFile;

    public bool IsVideo => MediaFileExtensions.IsVideo(FullPath);

    public static bool IsVideoPath(string path) => MediaFileExtensions.IsVideo(path);

    public bool ShowThumbnailLoading => IsVideo && IsThumbnailLoading && Thumbnail is null;

    partial void OnIsThumbnailLoadingChanged(bool value) =>
        OnPropertyChanged(nameof(ShowThumbnailLoading));

    partial void OnThumbnailChanged(ImageSource? value) =>
        OnPropertyChanged(nameof(ShowThumbnailLoading));
}