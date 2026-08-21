using System.IO;
using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class VideoThumbnailListItem : ObservableObject
{
    public required string ImagePath { get; init; }

    public string FileName => Path.GetFileName(ImagePath);

    [ObservableProperty]
    private ImageSource? _preview;
}
