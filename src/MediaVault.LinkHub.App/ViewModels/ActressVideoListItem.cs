using System.IO;
using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;

using MediaVault.LinkHub.Application.Media;
using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class ActressVideoListItem : ObservableObject
{
    public required MediaFile MediaFile { get; init; }

    public string Name => MediaFile.Name;

    public string FolderPath =>
        Path.GetDirectoryName(MediaFile.Path) ?? string.Empty;

    public string FolderName =>
        string.IsNullOrWhiteSpace(FolderPath)
            ? string.Empty
            : Path.GetFileName(FolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    public int RankingStars =>
        MediaFileRankingScale.ToDisplayStars(MediaFile.RankingGlobal);

    public string OpenCountLabel =>
        MediaFile.VecesAbierto == 1
            ? "1 apertura"
            : $"{MediaFile.VecesAbierto} aperturas";

    public string ActressesLabel =>
        MediaFile.Actresses.Count == 0
            ? string.Empty
            : string.Join(", ", MediaFile.Actresses.OrderBy(a => a.Name).Select(a => a.Name));

    public string CategoriesLabel =>
        MediaFile.Categories.Count == 0
            ? string.Empty
            : string.Join(", ", MediaFile.Categories.OrderBy(c => c.Name).Select(c => c.Name));

    public string ProducersLabel =>
        MediaFile.Producers.Count == 0
            ? string.Empty
            : string.Join(", ", MediaFile.Producers.OrderBy(p => p.Name).Select(p => p.Name));

    [ObservableProperty]
    private ImageSource? _thumbnail;
}
