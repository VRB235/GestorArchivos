using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaVault.LinkHub.App.Models;
using MediaVault.LinkHub.App.Navigation;
using MediaVault.LinkHub.App.Security;
using MediaVault.LinkHub.App.ViewModels.Base;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public MainViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        _navigationService.CurrentViewModelChanged += OnCurrentViewModelChanged;

        NavigationItems =
        [
            new NavigationItem { Title = "Dashboard", Icon = "📊", Target = "dashboard" },
            new NavigationItem { Title = "Link Manager", Icon = "🔗", Target = "links" },
            new NavigationItem { Title = "Media Vault", Icon = "🗂️", Target = "vault" },
            new NavigationItem { Title = "Categorías", Icon = "🏷️", Target = "categories" },
            new NavigationItem { Title = "Scratchpad", Icon = "📝", Target = "notes" },
            new NavigationItem { Title = "Configuración", Icon = "⚙️", Target = "settings" }
        ];
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    [ObservableProperty]
    private string _selectedNavigationTarget = "dashboard";

    public ViewModelBase? CurrentViewModel => _navigationService.CurrentViewModel;

    public string CurrentTitle =>
        (_navigationService.CurrentViewModel as INavigableViewModel)?.Title ?? "MediaVault & LinkHub";

    public string CurrentSubtitle =>
        (_navigationService.CurrentViewModel as INavigableViewModel)?.Subtitle ?? string.Empty;

    public bool IsMaintenanceMode => AppSecurityContext.IsMaintenanceMode;

    public async Task InitializeAsync()
    {
        SelectedNavigationTarget = "dashboard";
        await _navigationService.NavigateToAsync<DashboardViewModel>().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task NavigateAsync(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return;

        SelectedNavigationTarget = target;

        switch (target)
        {
            case "dashboard":
                await _navigationService.NavigateToAsync<DashboardViewModel>().ConfigureAwait(true);
                break;
            case "links":
                await _navigationService.NavigateToAsync<LinkManagerViewModel>().ConfigureAwait(true);
                break;
            case "vault":
                await _navigationService.NavigateToAsync<MediaVaultViewModel>().ConfigureAwait(true);
                break;
            case "categories":
                await _navigationService.NavigateToAsync<VideoCategoryManagerViewModel>().ConfigureAwait(true);
                break;
            case "notes":
                await _navigationService.NavigateToAsync<ScratchpadViewModel>().ConfigureAwait(true);
                break;
            case "settings":
                await _navigationService.NavigateToAsync<SettingsViewModel>().ConfigureAwait(true);
                break;
        }
    }

    private void OnCurrentViewModelChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CurrentViewModel));
        OnPropertyChanged(nameof(CurrentTitle));
        OnPropertyChanged(nameof(CurrentSubtitle));
    }
}
