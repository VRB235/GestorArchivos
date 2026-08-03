using CommunityToolkit.Mvvm.ComponentModel;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class FileCategorySelectionItem : ObservableObject
{
    public required int CategoryId { get; init; }

    public required string Name { get; init; }

    [ObservableProperty]
    private bool _isSelected;

    public Action? SelectionChanged { get; set; }

    partial void OnIsSelectedChanged(bool value) =>
        SelectionChanged?.Invoke();
}
