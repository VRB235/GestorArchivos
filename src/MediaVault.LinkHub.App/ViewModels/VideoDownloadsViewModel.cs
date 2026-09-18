using System.Collections.ObjectModel;
using System.IO;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using MediaVault.LinkHub.App.ViewModels.Base;
using MediaVault.LinkHub.Application.Models.Downloads;
using MediaVault.LinkHub.Application.Services;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class VideoDownloadsViewModel : ViewModelBase, INavigableViewModel, IDisposable
{
    private readonly IVideoDownloadService _videoDownloadService;
    private bool _disposed;

    public VideoDownloadsViewModel(IVideoDownloadService videoDownloadService)
    {
        _videoDownloadService = videoDownloadService;
        _videoDownloadService.ProgressChanged += OnProgressChanged;
    }

    public string Title => "Descargas";

    public string Subtitle =>
        "Pegue una URL HLS (.m3u8); se guarda el historial, el progreso y el archivo local (tokens con caducidad).";

    public ObservableCollection<VideoDownloadListItem> Downloads { get; } = [];

    [ObservableProperty]
    private string _urlInput = string.Empty;

    [ObservableProperty]
    private string? _displayNameInput;

    [ObservableProperty]
    private VideoDownloadListItem? _selectedDownload;

    public async Task InitializeAsync()
    {
        await RunBusyCoreAsync(async () =>
        {
            await _videoDownloadService.RecoverInterruptedAsync().ConfigureAwait(true);
            await ReloadAsync().ConfigureAwait(true);
        }, "Cargando descargas...").ConfigureAwait(true);
    }

    private async Task ReloadAsync(int? reselectId = null)
    {
        var items = await _videoDownloadService.GetAllAsync().ConfigureAwait(true);
        Downloads.Clear();
        foreach (var entity in items)
            Downloads.Add(new VideoDownloadListItem(entity));

        if (reselectId.HasValue)
            SelectedDownload = Downloads.FirstOrDefault(item => item.Id == reselectId.Value);
    }

    [RelayCommand]
    private async Task EnqueueAsync()
    {
        if (string.IsNullOrWhiteSpace(UrlInput))
        {
            ErrorMessage = "Pegue una URL http(s) válida.";
            return;
        }

        await ExecuteBusyAsync(async () =>
        {
            var created = await _videoDownloadService
                .EnqueueAsync(UrlInput, DisplayNameInput)
                .ConfigureAwait(true);

            UrlInput = string.Empty;
            DisplayNameInput = null;
            await ReloadAsync(created.Id).ConfigureAwait(true);
            StatusMessage = "Descarga encolada.";
        }, "Encolando...").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task RetryAsync(VideoDownloadListItem? item)
    {
        item ??= SelectedDownload;
        if (item is null || !item.CanRetry)
            return;

        await ExecuteBusyAsync(async () =>
        {
            await _videoDownloadService.RetryAsync(item.Id).ConfigureAwait(true);
            await ReloadAsync(item.Id).ConfigureAwait(true);
        }, "Reintentando...").ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task CancelAsync(VideoDownloadListItem? item)
    {
        item ??= SelectedDownload;
        if (item is null || !item.CanCancel)
            return;

        await _videoDownloadService.CancelAsync(item.Id).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task DeleteAsync(VideoDownloadListItem? item)
    {
        item ??= SelectedDownload;
        if (item is null)
            return;

        var id = item.Id;
        await ExecuteBusyAsync(async () =>
        {
            await _videoDownloadService.DeleteAsync(id, deleteFile: true).ConfigureAwait(true);
            if (SelectedDownload?.Id == id)
                SelectedDownload = null;
            await ReloadAsync().ConfigureAwait(true);
        }, "Eliminando...").ConfigureAwait(true);
    }

    [RelayCommand]
    private void Open(VideoDownloadListItem? item)
    {
        item ??= SelectedDownload;
        if (item is null)
            return;

        if (!_videoDownloadService.TryOpen(item.Id))
            ErrorMessage = "No se pudo abrir el video. Verifique que el archivo exista y haya un reproductor asociado (.ts).";
        else
            ErrorMessage = null;
    }

    [RelayCommand]
    private void OpenFolder(VideoDownloadListItem? item)
    {
        item ??= SelectedDownload;
        if (item is null || string.IsNullOrWhiteSpace(item.LocalPath))
            return;

        var path = item.LocalPath;
        if (!File.Exists(path))
        {
            ErrorMessage = "El archivo local ya no existe.";
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true
            });
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo abrir la carpeta: {ex.Message}";
        }
    }

    private void OnProgressChanged(object? sender, VideoDownloadProgressUpdate update)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        if (dispatcher.CheckAccess())
            ApplyProgress(update);
        else
            dispatcher.Invoke(() => ApplyProgress(update));
    }

    private void ApplyProgress(VideoDownloadProgressUpdate update)
    {
        var item = Downloads.FirstOrDefault(download => download.Id == update.Id);
        if (item is null)
            return;

        item.Apply(update);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _videoDownloadService.ProgressChanged -= OnProgressChanged;
    }
}
