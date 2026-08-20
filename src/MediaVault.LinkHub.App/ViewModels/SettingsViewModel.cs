using System.Diagnostics;
using System.IO;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MediaVault.LinkHub.App.Shell;
using MediaVault.LinkHub.App.ViewModels.Base;
using MediaVault.LinkHub.Application.Models.Settings;
using MediaVault.LinkHub.Application.Services;

using Microsoft.Win32;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class SettingsViewModel : ViewModelBase, INavigableViewModel
{
    private readonly IAppSettingsService _appSettingsService;
    private readonly IMediaVaultService _mediaVaultService;
    private readonly ISqliteDatabaseBackupService _backupService;
    private readonly IAppDialogService _appDialogService;

    public SettingsViewModel(
        IAppSettingsService appSettingsService,
        IMediaVaultService mediaVaultService,
        ISqliteDatabaseBackupService backupService,
        IAppDialogService appDialogService)
    {
        _appSettingsService = appSettingsService;
        _mediaVaultService = mediaVaultService;
        _backupService = backupService;
        _appDialogService = appDialogService;
    }

    public string Title => "Configuración";

    public string Subtitle => "Rutas y preferencias persistentes de la aplicación";

    [ObservableProperty]
    private string _mediaIndexRootPath = string.Empty;

    [ObservableProperty]
    private string? _settingsFilePath;

    [ObservableProperty]
    private string? _backupDirectoryPath;

    [ObservableProperty]
    private string? _latestBackupSummary;

    public Task InitializeAsync() =>
        RunBusyCoreAsync(LoadAsync, "Cargando configuración...");

    private async Task LoadAsync()
    {
        var settings = await _appSettingsService.GetAsync().ConfigureAwait(true);
        MediaIndexRootPath = settings.MediaIndexRootPath;
        SettingsFilePath = _appSettingsService.GetSettingsFilePath();
        RefreshBackupSummary();
    }

    private void RefreshBackupSummary()
    {
        BackupDirectoryPath = _backupService.GetBackupDirectory();
        var latest = _backupService.ListBackups().FirstOrDefault();
        LatestBackupSummary = latest is null
            ? "Aún no hay respaldos."
            : $"Último: {latest.FileName} ({latest.CreatedUtc.ToLocalTime():g}, {latest.SizeBytes / 1024.0:0} KB)";
    }

    [RelayCommand]
    private void BrowseMediaIndexFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Seleccionar carpeta raíz para indexación multimedia"
        };

        if (!string.IsNullOrWhiteSpace(MediaIndexRootPath) && Directory.Exists(MediaIndexRootPath))
            dialog.InitialDirectory = MediaIndexRootPath;

        if (dialog.ShowDialog() == true)
            MediaIndexRootPath = dialog.FolderName;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(MediaIndexRootPath))
        {
            ErrorMessage = "Indique la carpeta raíz de indexación multimedia.";
            return;
        }

        if (!Directory.Exists(MediaIndexRootPath))
        {
            ErrorMessage = "La carpeta indicada no existe.";
            return;
        }

        await ExecuteBusyAsync(async () =>
        {
            var normalizedPath = Path.GetFullPath(MediaIndexRootPath);
            var current = await _appSettingsService.GetAsync().ConfigureAwait(true);
            await _appSettingsService.SaveAsync(new AppSettings
            {
                MediaIndexRootPath = normalizedPath,
                FolderIconPaths = current.FolderIconPaths,
                ShowHiddenFilesAndFolders = current.ShowHiddenFilesAndFolders
            }).ConfigureAwait(true);

            MediaIndexRootPath = normalizedPath;
            SettingsFilePath = _appSettingsService.GetSettingsFilePath();
        }, "Guardando configuración...").ConfigureAwait(true);
    }

    [RelayCommand]
    private void OpenBackupsFolder()
    {
        var directory = _backupService.GetBackupDirectory();
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private async Task CreateBackupNowAsync()
    {
        var result = await ExecuteBusyAsync(
            () => _backupService.CreateBackupAsync("manual"),
            "Creando respaldo...").ConfigureAwait(true);

        if (ErrorMessage is not null || result is null)
            return;

        RefreshBackupSummary();
        _appDialogService.ShowMessage(
            "Respaldo",
            result.Created
                ? $"Respaldo creado:\n{result.BackupFilePath}"
                : result.Message ?? "No se creó respaldo.");
    }

    [RelayCommand]
    private async Task RestoreLatestBackupAsync()
    {
        var latest = _backupService.ListBackups().FirstOrDefault();
        if (latest is null)
        {
            _appDialogService.ShowMessage(
                "Restaurar respaldo",
                "No hay respaldos disponibles.",
                AppDialogKind.Warning);
            return;
        }

        if (!_appDialogService.ConfirmYesNo(
                "Restaurar respaldo",
                "¿Restaurar la base de datos desde el respaldo más reciente?\n\n" +
                $"{latest.FileName}\n" +
                $"Fecha: {latest.CreatedUtc.ToLocalTime():g}\n\n" +
                "La app se cerrará y deberá volver a abrirla para aplicar la restauración. " +
                "Se guardará una copia del estado actual como pre-restore.",
                AppDialogKind.Warning))
            return;

        await ExecuteBusyAsync(
            () => _backupService.StageRestoreAsync(latest.FilePath),
            "Programando restauración...").ConfigureAwait(true);

        if (ErrorMessage is not null)
            return;

        _appDialogService.ShowMessage(
            "Restauración programada",
            "Cierre la aplicación y ábrala de nuevo para completar la restauración.");

        System.Windows.Application.Current.Shutdown();
    }

    [RelayCommand]
    private async Task PurgeInvalidIndexEntriesAsync()
    {
        if (!_appDialogService.ConfirmYesNo(
                "Confirmar limpieza del índice",
                "¿Eliminar del índice las entradas inválidas?\n\n" +
                "Se quitarán rutas de la papelera/sistema, fuera de la carpeta raíz y archivos que ya no existen en disco. " +
                "También se pierden aperturas, rankings y tags asociados a esas entradas. " +
                "Antes de borrar se crea un respaldo automático en Backups. " +
                "No se borra nada del disco.",
                AppDialogKind.Warning))
            return;

        var result = await ExecuteBusyAsync(
            async () =>
            {
                var settings = await _appSettingsService.GetAsync().ConfigureAwait(true);
                return await _mediaVaultService
                    .PurgeInvalidIndexEntriesAsync(
                        settings.MediaIndexRootPath,
                        removeMissingFiles: true)
                    .ConfigureAwait(true);
            },
            "Limpiando índice inválido...").ConfigureAwait(true);

        if (ErrorMessage is not null || result is null)
        {
            RefreshBackupSummary();
            return;
        }

        RefreshBackupSummary();
        _appDialogService.ShowMessage(
            "Limpieza del índice",
            result.HasChanges
                ? $"Entradas eliminadas: {result.RemovedTotal}\n" +
                  $"• Papelera/sistema: {result.RemovedUnusablePaths}\n" +
                  $"• Fuera del root: {result.RemovedOutsideRoot}\n" +
                  $"• Inexistentes en disco: {result.RemovedMissingFiles}" +
                  (string.IsNullOrWhiteSpace(result.BackupFilePath)
                      ? string.Empty
                      : $"\n\nRespaldo: {result.BackupFilePath}")
                : "No había entradas inválidas que limpiar.");
    }

    [RelayCommand]
    private async Task ClearAllMediaMetadataAsync()
    {
        if (!_appDialogService.ConfirmYesNo(
                "Confirmar limpieza de metadatos",
                "¿Restablecer todos los metadatos de seguimiento?\n\n" +
                "Se borrarán rankings, contador de aperturas, asignaciones de categorías/actrices/productoras y " +
                "los catálogos correspondientes. " +
                "Antes se crea un respaldo automático. " +
                "Los archivos en disco no se eliminan.",
                AppDialogKind.Warning))
            return;

        var result = await ExecuteBusyAsync(
            () => _mediaVaultService.ClearAllMediaMetadataAsync(),
            "Limpiando metadatos...").ConfigureAwait(true);

        if (ErrorMessage is not null || result is null)
        {
            RefreshBackupSummary();
            return;
        }

        RefreshBackupSummary();
        _appDialogService.ShowMessage(
            "Limpieza de metadatos",
            result.HasChanges
                ? $"Archivos restablecidos: {result.FilesUpdated}\n" +
                  $"Asignaciones de categoría eliminadas: {result.CategoryLinksRemoved}\n" +
                  $"Categorías eliminadas: {result.CategoriesDeleted}\n" +
                  $"Asignaciones de actriz eliminadas: {result.ActressLinksRemoved}\n" +
                  $"Actrices eliminadas: {result.ActressesDeleted}\n" +
                  $"Asignaciones de productora eliminadas: {result.ProducerLinksRemoved}\n" +
                  $"Productoras eliminadas: {result.ProducersDeleted}" +
                  (string.IsNullOrWhiteSpace(result.BackupFilePath)
                      ? string.Empty
                      : $"\n\nRespaldo: {result.BackupFilePath}")
                : "No había metadatos de seguimiento que restablecer.");
    }
}
