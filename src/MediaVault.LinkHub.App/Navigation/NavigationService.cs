using MediaVault.LinkHub.App.ViewModels.Base;
using Microsoft.Extensions.DependencyInjection;

namespace MediaVault.LinkHub.App.Navigation;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ViewModelBase? CurrentViewModel { get; private set; }

    public event EventHandler? CurrentViewModelChanged;

    public async Task NavigateToAsync<TViewModel>() where TViewModel : ViewModelBase
    {
        var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
        CurrentViewModel = viewModel;
        CurrentViewModelChanged?.Invoke(this, EventArgs.Empty);

        if (viewModel is INavigableViewModel navigable)
            await navigable.InitializeAsync().ConfigureAwait(true);
    }
}
