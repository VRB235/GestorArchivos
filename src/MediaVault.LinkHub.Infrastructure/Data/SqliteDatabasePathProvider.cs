namespace MediaVault.LinkHub.Infrastructure.Data;

/// <summary>
/// Resuelve la ubicación del archivo SQLite en el perfil local del usuario,
/// aislando desarrollo (Debug) de producción (Release).
/// </summary>
public static class SqliteDatabasePathProvider
{
    public const string ProductionAppFolderName = "MediaVaultLinkHub";
    public const string DevelopmentAppFolderName = "MediaVaultLinkHub.Development";
    public const string DatabaseFileName = "mediavault_linkhub.db";

    /// <summary>Variable de entorno opcional: <c>Production</c> o <c>Development</c>.</summary>
    public const string EnvironmentVariableName = "MEDIAVAULT_ENVIRONMENT";

    public static bool IsDevelopment =>
        ResolveEnvironmentName().Equals("Development", StringComparison.OrdinalIgnoreCase);

    public static string GetEnvironmentName() => ResolveEnvironmentName();

    public static string GetAppFolderName() =>
        IsDevelopment ? DevelopmentAppFolderName : ProductionAppFolderName;

    public static string GetAppDataDirectory()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDirectory = Path.Combine(basePath, GetAppFolderName());
        Directory.CreateDirectory(appDirectory);
        return appDirectory;
    }

    /// <summary>Carpeta de datos de producción (siempre <c>MediaVaultLinkHub</c>), sin crear directorio.</summary>
    public static string GetProductionAppDataDirectory()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(basePath, ProductionAppFolderName);
    }

    public static string GetDefaultDatabasePath() =>
        Path.Combine(GetAppDataDirectory(), DatabaseFileName);

    public static string GetProductionDatabasePath() =>
        Path.Combine(GetProductionAppDataDirectory(), DatabaseFileName);

    public static string BuildConnectionString(string? databasePath = null)
    {
        var path = databasePath ?? GetDefaultDatabasePath();
        return $"Data Source={path}";
    }

    private static string ResolveEnvironmentName()
    {
        var configured = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim().Equals("Development", StringComparison.OrdinalIgnoreCase)
                ? "Development"
                : "Production";
        }

#if DEBUG
        return "Development";
#else
        return "Production";
#endif
    }
}
