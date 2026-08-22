using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MediaVault.LinkHub.App.Shell;
using MediaVault.LinkHub.App.ViewModels.Base;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class VideoCategoryManagerViewModel : ViewModelBase, INavigableViewModel
{
    private readonly IVideoCategoryService _videoCategoryService;
    private readonly IAppDialogService _appDialogService;

    public VideoCategoryManagerViewModel(
        IVideoCategoryService videoCategoryService,
        IAppDialogService appDialogService)
    {
        _videoCategoryService = videoCategoryService;
        _appDialogService = appDialogService;
    }

    public string Title => "Categorías";

    public string Subtitle => "Clasificación de archivos indexados (indexación en Configuración)";

    public ObservableCollection<VideoCategory> VideoCategories { get; } = [];

    [ObservableProperty]
    private VideoCategory? _selectedCategory;

    [ObservableProperty]
    private string _categoryName = string.Empty;

    public bool CanEditCategory => SelectedCategory is not null;

    public Task InitializeAsync() =>
        RunBusyCoreAsync(() => ReloadAsync(), "Cargando categorías...");

    private async Task ReloadAsync()
    {
        var categories = await _videoCategoryService.GetAllAsync().ConfigureAwait(true);

        VideoCategories.Clear();
        foreach (var category in categories)
            VideoCategories.Add(category);

        NotifyCategoryCommands();
    }

    partial void OnSelectedCategoryChanged(VideoCategory? value)
    {
        CategoryName = value?.Name ?? string.Empty;
        NotifyCategoryCommands();
    }

    private void NotifyCategoryCommands()
    {
        OnPropertyChanged(nameof(CanEditCategory));
        RenameCategoryCommand.NotifyCanExecuteChanged();
        DeleteCategoryCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(CategoryName))
        {
            ErrorMessage = "Indique un nombre para la categoría.";
            return;
        }

        try
        {
            ErrorMessage = null;
            await _videoCategoryService.CreateAsync(CategoryName).ConfigureAwait(true);
            CategoryName = string.Empty;
            SelectedCategory = null;
            await ReloadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditCategory))]
    private async Task RenameCategoryAsync()
    {
        if (SelectedCategory is null || string.IsNullOrWhiteSpace(CategoryName))
            return;

        try
        {
            ErrorMessage = null;
            await _videoCategoryService.UpdateAsync(SelectedCategory.Id, CategoryName).ConfigureAwait(true);
            await ReloadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditCategory))]
    private async Task DeleteCategoryAsync()
    {
        if (SelectedCategory is null)
            return;

        if (!_appDialogService.ConfirmYesNo(
                "Confirmar eliminación",
                $"¿Eliminar la categoría «{SelectedCategory.Name}»?\n\nLos archivos quedarán sin categoría.",
                AppDialogKind.Question))
            return;

        try
        {
            ErrorMessage = null;
            await _videoCategoryService.DeleteAsync(SelectedCategory.Id).ConfigureAwait(true);
            SelectedCategory = null;
            CategoryName = string.Empty;
            await ReloadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
