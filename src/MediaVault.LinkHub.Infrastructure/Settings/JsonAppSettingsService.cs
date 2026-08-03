using System.Text.Json;

using MediaVault.LinkHub.Application.Models.Settings;
using MediaVault.LinkHub.Application.Services;
using MediaVault.LinkHub.Infrastructure.Data;

namespace MediaVault.LinkHub.Infrastructure.Settings;

public sealed class JsonAppSettingsService : IAppSettingsService
{
    private const string SettingsFileName = "appsettings.user.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _sync = new(1, 1);

    public string GetSettingsFilePath() =>
        Path.Combine(SqliteDatabasePathProvider.GetAppDataDirectory(), SettingsFileName);

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var path = GetSettingsFilePath();
            if (!File.Exists(path))
                return new AppSettings();

            await using var stream = File.OpenRead(path);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);

            return NormalizeSettings(settings ?? new AppSettings());
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task<string?> GetFolderIconPathAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return null;

        var settings = await GetAsync(cancellationToken).ConfigureAwait(false);
        var normalizedFolder = Path.GetFullPath(folderPath);
        return settings.FolderIconPaths.TryGetValue(normalizedFolder, out var iconPath)
            ? iconPath
            : null;
    }

    public async Task SaveFolderIconAsync(
        string folderPath,
        string? iconPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            throw new ArgumentException("La ruta de la carpeta es obligatoria.", nameof(folderPath));

        var settings = await GetAsync(cancellationToken).ConfigureAwait(false);
        var normalizedFolder = Path.GetFullPath(folderPath);
        var folderIcons = new Dictionary<string, string>(settings.FolderIconPaths, StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(iconPath))
        {
            folderIcons.Remove(normalizedFolder);
        }
        else
        {
            if (!File.Exists(iconPath))
                throw new FileNotFoundException("No se encontró la imagen del icono.", iconPath);

            folderIcons[normalizedFolder] = Path.GetFullPath(iconPath);
        }

        await SaveAsync(new AppSettings
        {
            MediaIndexRootPath = settings.MediaIndexRootPath,
            FolderIconPaths = folderIcons,
            ShowHiddenFilesAndFolders = settings.ShowHiddenFilesAndFolders
        }, cancellationToken).ConfigureAwait(false);
    }

    private static AppSettings NormalizeSettings(AppSettings settings) =>
        new()
        {
            MediaIndexRootPath = settings.MediaIndexRootPath ?? string.Empty,
            FolderIconPaths = new Dictionary<string, string>(
                settings.FolderIconPaths ?? new Dictionary<string, string>(),
                StringComparer.OrdinalIgnoreCase),
            ShowHiddenFilesAndFolders = settings.ShowHiddenFilesAndFolders
        };

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _sync.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var path = GetSettingsFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _sync.Release();
        }
    }
}
