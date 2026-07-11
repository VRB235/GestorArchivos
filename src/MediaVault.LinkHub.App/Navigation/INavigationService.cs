using MediaVault.LinkHub.App.ViewModels.Base;

namespace MediaVault.LinkHub.App.Navigation;

public interface INavigationService
{
    ViewModelBase? CurrentViewModel { get; }

    event EventHandler? CurrentViewModelChanged;

    Task NavigateToAsync<TViewModel>() where TViewModel : ViewModelBase;
}
