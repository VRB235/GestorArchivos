using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Infrastructure.Data;
using MediaVault.LinkHub.Infrastructure.Services;
using MediaVault.LinkHub.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MediaVault.LinkHub.Infrastructure;

public static class DependencyInjection
{
  /// <summary>
  /// Registra DbContext, fábrica y servicios de aplicación en el contenedor DI.
  /// </summary>
  public static IServiceCollection AddMediaVaultLinkHubInfrastructure(
    this IServiceCollection services,
    string? databasePath = null)
  {
    services.AddDbContextFactory<AppDbContext>(options =>
      options.UseSqlite(SqliteDatabasePathProvider.BuildConnectionString(databasePath)));

    services.AddSingleton<IAppSettingsService, JsonAppSettingsService>();

    services.AddTransient<IWebLinkService, WebLinkService>();
    services.AddTransient<IMediaVaultService, MediaVaultService>();
    services.AddTransient<IDashboardService, DashboardService>();
    services.AddTransient<IQuickNoteService, QuickNoteService>();
    services.AddTransient<IVideoCategoryService, VideoCategoryService>();

    return services;
  }

  /// <summary>
  /// Aplica migraciones pendientes y migra logos externos al almacén managed.
  /// </summary>
  public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
  {
    await using var scope = serviceProvider.CreateAsyncScope();
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
    await context.InitializeAsync(cancellationToken).ConfigureAwait(false);

    var webLinkService = scope.ServiceProvider.GetRequiredService<IWebLinkService>();
    await webLinkService.MigrateExternalLogosAsync(cancellationToken).ConfigureAwait(false);
  }
}
