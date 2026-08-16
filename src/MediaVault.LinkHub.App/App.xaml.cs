using System.IO;
using System.Windows;
using System.Windows.Threading;

using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

using MediaVault.LinkHub.App.ViewModels;
using MediaVault.LinkHub.App.Security;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Infrastructure;
using MediaVault.LinkHub.Infrastructure.Data;

using Microsoft.Extensions.DependencyInjection;

namespace MediaVault.LinkHub.App;

public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        HookGlobalExceptionHandlers();

        // Evita que la app se cierre al cerrar el diálogo de PIN (antes de abrir MainWindow).
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        LiveCharts.Configure(config =>
            config.AddSkiaSharp().AddDefaultMappers());

#if DEBUG
        // En desarrollo (Debug) se omite PIN/contraseña para agilizar iteración.
        AppSecurityContext.AccessMode = SecurityAccessMode.Full;
#else
        var securityGate = new Views.SecurityGateWindow();
        if (securityGate.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        AppSecurityContext.AccessMode = securityGate.AccessMode;
#endif

        try
        {
            var services = new ServiceCollection();
            services.AddMediaVaultLinkHubInfrastructure();
            services.AddPresentation();

            Services = services.BuildServiceProvider();

            await Services.InitializeDatabaseAsync().ConfigureAwait(true);

            // Limpia papelera / fuera del vault / inexistentes antes de cargar el Dashboard.
            await PurgeInvalidIndexOnStartupAsync().ConfigureAwait(true);

            // Mostrar la ventana antes de cargar el Dashboard: feedback inmediato tras el login.
            var mainViewModel = Services.GetRequiredService<MainViewModel>();
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = mainViewModel;
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();

            await mainViewModel.InitializeAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            TryLogStartupFailure(ex);
            System.Windows.MessageBox.Show(
                $"No se pudo iniciar la aplicación.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "MediaVault & LinkHub",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void HookGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        TryLogStartupFailure(e.Exception);
        e.Handled = true;
        System.Windows.MessageBox.Show(
            $"Ocurrió un error inesperado.{Environment.NewLine}{Environment.NewLine}{e.Exception.Message}",
            "MediaVault & LinkHub",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            TryLogStartupFailure(ex);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        TryLogStartupFailure(e.Exception);
        e.SetObserved();
    }

    private static async Task PurgeInvalidIndexOnStartupAsync()
    {
        try
        {
            var settings = await Services.GetRequiredService<IAppSettingsService>()
                .GetAsync()
                .ConfigureAwait(true);
            var vault = Services.GetRequiredService<IMediaVaultService>();
            var result = await vault
                .PurgeInvalidIndexEntriesAsync(settings.MediaIndexRootPath, removeMissingFiles: true)
                .ConfigureAwait(true);

            if (result.HasChanges)
            {
                TryLogStartupInfo(
                    $"Índice depurado al arranque: total={result.RemovedTotal} " +
                    $"(papelera/sistema={result.RemovedUnusablePaths}, fueraRoot={result.RemovedOutsideRoot}, " +
                    $"inexistentes={result.RemovedMissingFiles}).");
            }
        }
        catch (Exception ex)
        {
            // No bloquear el arranque si la depuración falla.
            TryLogStartupFailure(ex);
        }
    }

    private static void TryLogStartupInfo(string message)
    {
        try
        {
            var directory = SqliteDatabasePathProvider.GetAppDataDirectory();
            var logPath = Path.Combine(directory, "startup-errors.log");
            File.AppendAllText(
                logPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO {message}{Environment.NewLine}");
        }
        catch
        {
            // ignore
        }
    }

    private static void TryLogStartupFailure(Exception ex)
    {
        try
        {
            var directory = SqliteDatabasePathProvider.GetAppDataDirectory();
            var logPath = Path.Combine(directory, "startup-errors.log");
            File.AppendAllText(
                logPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Sin I/O de log no se debe tumbar el manejo de error.
        }
    }
}
