using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Infrastructure.Data;
using MediaVault.LinkHub.Infrastructure.Media;
using MediaVault.LinkHub.Infrastructure.Scraping;
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

    services.AddSingleton<ISqliteDatabaseBackupService>(_ =>
      new SqliteDatabaseBackupService(
        databasePath: databasePath ?? SqliteDatabasePathProvider.GetDefaultDatabasePath()));
    services.AddSingleton<IAppSettingsService, JsonAppSettingsService>();
    services.AddSingleton<IRankedVideoRecommendationSession, RankedVideoRecommendationSession>();

    services.AddSingleton<SuggestionImageStorage>();
    services.AddHttpClient("VideoScraper", client =>
    {
      client.Timeout = TimeSpan.FromSeconds(60);
      client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
      client.DefaultRequestHeaders.Accept.ParseAdd(
        "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
      client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
      AutomaticDecompression = System.Net.DecompressionMethods.All,
      UseCookies = true,
      CookieContainer = new System.Net.CookieContainer()
    });

    services.AddTransient<IVideoPageScraper>(sp =>
      new CssListVideoPageScraper(
        sp.GetRequiredService<System.Net.Http.IHttpClientFactory>(),
        sp.GetService<IBrowserHtmlFetcher>()));
    services.AddTransient<IWebLinkService, WebLinkService>();
    services.AddTransient<IMediaVaultService, MediaVaultService>();
    services.AddTransient<IDashboardService, DashboardService>();
    services.AddTransient<IQuickNoteService, QuickNoteService>();
    services.AddTransient<ISuggestionService, SuggestionService>();
    services.AddTransient<IVideoCategoryService, VideoCategoryService>();
    services.AddTransient<IActressService, ActressService>();
    services.AddTransient<IActressLinkService, ActressLinkService>();
    services.AddTransient<IVideoScrapeService, VideoScrapeService>();
    services.AddTransient<IProducerService, ProducerService>();

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
