using System.Windows;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using MediaVault.LinkHub.App.ViewModels;
using MediaVault.LinkHub.App.Security;
using MediaVault.LinkHub.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MediaVault.LinkHub.App;

public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Evita que la app se cierre al cerrar el diálogo de PIN (antes de abrir MainWindow).
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        LiveCharts.Configure(config =>
            config.AddSkiaSharp().AddDefaultMappers());

        var securityGate = new Views.SecurityGateWindow();
        if (securityGate.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        AppSecurityContext.AccessMode = securityGate.AccessMode;

        try
        {
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
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"No se pudo iniciar la aplicación.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "MediaVault & LinkHub",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }
}
