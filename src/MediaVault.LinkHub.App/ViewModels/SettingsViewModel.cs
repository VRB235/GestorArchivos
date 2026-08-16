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
    private readonly IAppDialogService _appDialogService;

    public SettingsViewModel(
        IAppSettingsService appSettingsService,
        IMediaVaultService mediaVaultService,
        IAppDialogService appDialogService)
    {
        _appSettingsService = appSettingsService;
        _mediaVaultService = mediaVaultService;
        _appDialogService = appDialogService;
    }

    public string Title => "Configuración";

    public string Subtitle => "Rutas y preferencias persistentes de la aplicación";

    [ObservableProperty]
    private string _mediaIndexRootPath = string.Empty;

    [ObservableProperty]
    private string? _settingsFilePath;

    public Task InitializeAsync() =>
        RunBusyCoreAsync(LoadAsync, "Cargando configuración...");

    private async Task LoadAsync()
    {
        var settings = await _appSettingsService.GetAsync().ConfigureAwait(true);
        MediaIndexRootPath = settings.MediaIndexRootPath;
        SettingsFilePath = _appSettingsService.GetSettingsFilePath();
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
    private async Task PurgeInvalidIndexEntriesAsync()
    {
        if (!_appDialogService.ConfirmYesNo(
                "Confirmar limpieza del índice",
                "¿Eliminar del índice las entradas inválidas?\n\n" +
                "Se quitarán rutas de la papelera/sistema, fuera de la carpeta raíz y archivos que ya no existen en disco. " +
                "No se borra nada del disco. Esta acción no se puede deshacer.",
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
            return;

        _appDialogService.ShowMessage(
            "Limpieza del índice",
            result.HasChanges
                ? $"Entradas eliminadas: {result.RemovedTotal}\n" +
                  $"• Papelera/sistema: {result.RemovedUnusablePaths}\n" +
                  $"• Fuera del root: {result.RemovedOutsideRoot}\n" +
                  $"• Inexistentes en disco: {result.RemovedMissingFiles}"
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
                "Los archivos en disco no se eliminan. Esta acción no se puede deshacer.",
                AppDialogKind.Warning))
            return;

        var result = await ExecuteBusyAsync(
            () => _mediaVaultService.ClearAllMediaMetadataAsync(),
            "Limpiando metadatos...").ConfigureAwait(true);

        if (ErrorMessage is not null || result is null)
            return;

        _appDialogService.ShowMessage(
            "Limpieza de metadatos",
            result.HasChanges
                ? $"Archivos restablecidos: {result.FilesUpdated}\n" +
                  $"Asignaciones de categoría eliminadas: {result.CategoryLinksRemoved}\n" +
                  $"Categorías eliminadas: {result.CategoriesDeleted}\n" +
                  $"Asignaciones de actriz eliminadas: {result.ActressLinksRemoved}\n" +
                  $"Actrices eliminadas: {result.ActressesDeleted}\n" +
                  $"Asignaciones de productora eliminadas: {result.ProducerLinksRemoved}\n" +
                  $"Productoras eliminadas: {result.ProducersDeleted}"
                : "No había metadatos de seguimiento que restablecer.");
    }
}
