using MediaVault.LinkHub.App.Navigation;
using MediaVault.LinkHub.App.Shell;
using MediaVault.LinkHub.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MediaVault.LinkHub.App;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IAppDialogService, AppDialogService>();
        services.AddSingleton<BrowserThumbnailLoader>();
        services.AddSingleton<MainViewModel>();

        services.AddTransient<LinkManagerViewModel>();
        services.AddTransient<MediaVaultViewModel>();
        services.AddTransient<VideoCategoryManagerViewModel>();
        services.AddTransient<ActressesViewModel>();
        services.AddTransient<ProducerManagerViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<ScratchpadViewModel>();
        services.AddTransient<SuggestionsViewModel>();
        services.AddTransient<SettingsViewModel>();

        services.AddTransient<MainWindow>();

        return services;
    }
}
