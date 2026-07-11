using MediaVault.LinkHub.Application.Models.Settings;

namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// Persistencia de preferencias de la aplicación (rutas y opciones fijas del usuario).
/// </summary>
public interface IAppSettingsService
{
    Task<AppSettings> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);

    Task<string?> GetFolderIconPathAsync(string folderPath, CancellationToken cancellationToken = default);

    Task SaveFolderIconAsync(
        string folderPath,
        string? iconPath,
        CancellationToken cancellationToken = default);

    string GetSettingsFilePath();
}
