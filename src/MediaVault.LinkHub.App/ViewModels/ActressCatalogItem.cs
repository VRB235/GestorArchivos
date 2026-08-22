using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;

using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class ActressCatalogItem : ObservableObject
{
    public required Actress Actress { get; init; }

    public int Id => Actress.Id;

    public string Name => Actress.Name;

    [ObservableProperty]
    private ImageSource? _thumbnail;
}
