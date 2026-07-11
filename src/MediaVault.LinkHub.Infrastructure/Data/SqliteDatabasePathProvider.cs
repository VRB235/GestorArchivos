namespace MediaVault.LinkHub.Infrastructure.Data;

/// <summary>
/// Resuelve la ubicación del archivo SQLite en el perfil local del usuario.
/// </summary>
public static class SqliteDatabasePathProvider
{
    public const string AppFolderName = "MediaVaultLinkHub";
    public const string DatabaseFileName = "mediavault_linkhub.db";

    public static string GetAppDataDirectory()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDirectory = Path.Combine(basePath, AppFolderName);
        Directory.CreateDirectory(appDirectory);
        return appDirectory;
    }

    public static string GetDefaultDatabasePath() =>
        Path.Combine(GetAppDataDirectory(), DatabaseFileName);

    public static string BuildConnectionString(string? databasePath = null)
    {
        var path = databasePath ?? GetDefaultDatabasePath();
        return $"Data Source={path}";
    }
}
