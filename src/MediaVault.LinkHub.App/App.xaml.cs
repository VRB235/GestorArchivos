using System.Windows;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using MediaVault.LinkHub.App.ViewModels;
using MediaVault.LinkHub.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MediaVault.LinkHub.App;

public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        LiveCharts.Configure(config =>
            config.AddSkiaSharp().AddDefaultMappers());

        var services = new ServiceCollection();
        services.AddMediaVaultLinkHubInfrastructure();
        services.AddPresentation();

        Services = services.BuildServiceProvider();

        await Services.InitializeDatabaseAsync().ConfigureAwait(true);

        var mainViewModel = Services.GetRequiredService<MainViewModel>();
        await mainViewModel.InitializeAsync().ConfigureAwait(true);

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.DataContext = mainViewModel;
        mainWindow.Show();
    }
}
