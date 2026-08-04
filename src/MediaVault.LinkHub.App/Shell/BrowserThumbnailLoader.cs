using System.Windows.Media;

using MediaVault.LinkHub.App.ViewModels;
using MediaVault.LinkHub.Infrastructure.Media;

namespace MediaVault.LinkHub.App.Shell;

/// <summary>
/// Carga miniaturas en segundo plano sin bloquear la navegación del explorador.
/// </summary>
public sealed class BrowserThumbnailLoader
{
    private const int MaxParallelLoads = 3;
    private readonly SemaphoreSlim _parallelGate = new(MaxParallelLoads, MaxParallelLoads);

    public int NextGeneration() =>
        Interlocked.Increment(ref _activeGeneration);

    private int _activeGeneration;

    public void BeginLoad(int generation, IEnumerable<MediaVaultBrowserEntryItem> items)
    {
        foreach (var item in items)
            _ = LoadItemAsync(item, generation);
    }

    public void LoadItem(MediaVaultBrowserEntryItem item, int generation) =>
        _ = LoadItemAsync(item, generation);

    private async Task LoadItemAsync(MediaVaultBrowserEntryItem item, int generation)
    {
        if (generation != Volatile.Read(ref _activeGeneration))
            return;

        if (item.Thumbnail is not null)
            return;

        if (item.IsDirectory)
        {
            await LoadDirectoryThumbnailAsync(item, generation).ConfigureAwait(false);
            return;
        }

        var isVideo = MediaFileExtensions.IsVideo(item.FullPath);

        if (isVideo)
            await SetThumbnailLoadingAsync(item, true).ConfigureAwait(false);

        await _parallelGate.WaitAsync().ConfigureAwait(false);

        try
        {
            if (generation != Volatile.Read(ref _activeGeneration))
                return;

            ImageSource? thumbnail = null;
            var maxAttempts = isVideo ? 10 : 1;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (generation != Volatile.Read(ref _activeGeneration))
                    return;

                if (attempt > 0 && isVideo)
                    WindowsShellThumbnailProvider.InvalidateCacheEntry(item.FullPath, isDirectory: false);

                thumbnail = await WindowsShellThumbnailProvider
                    .GetThumbnailAsync(item.FullPath, false)
                    .ConfigureAwait(false);

                if (thumbnail is not null)
                    break;

                if (!isVideo || attempt == maxAttempts - 1)
                    break;

                await Task.Delay(attempt switch
                {
                    0 => 500,
                    1 => 1000,
                    2 => 1500,
                    3 => 2000,
                    _ => 2500
                }).ConfigureAwait(false);
            }

            if (generation != Volatile.Read(ref _activeGeneration))
                return;

            if (thumbnail is not null)
                await ApplyThumbnailAsync(item, thumbnail).ConfigureAwait(false);
        }
        finally
        {
            _parallelGate.Release();

            if (isVideo)
                await SetThumbnailLoadingAsync(item, false).ConfigureAwait(false);
        }
    }

    private async Task LoadDirectoryThumbnailAsync(MediaVaultBrowserEntryItem item, int generation)
    {
        ImageSource? thumbnail = await Task.Run(() =>
            FolderSessionPicturePicker.TryLoadSessionThumbnail(item.FullPath)).ConfigureAwait(false);

        if (thumbnail is null && !string.IsNullOrWhiteSpace(item.Entry.CustomIconPath))
        {
            thumbnail = await Task.Run(() =>
                LocalImageLoader.TryLoad(item.Entry.CustomIconPath)).ConfigureAwait(false);
        }

        if (generation != Volatile.Read(ref _activeGeneration))
            return;

        if (thumbnail is not null)
            await ApplyThumbnailAsync(item, thumbnail).ConfigureAwait(false);
    }

    private static Task ApplyThumbnailAsync(MediaVaultBrowserEntryItem item, ImageSource thumbnail)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return Task.CompletedTask;

        if (dispatcher.CheckAccess())
        {
            item.Thumbnail = thumbnail;
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(() => item.Thumbnail = thumbnail).Task;
    }

    private static Task SetThumbnailLoadingAsync(MediaVaultBrowserEntryItem item, bool isLoading)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
            return Task.CompletedTask;

        if (dispatcher.CheckAccess())
        {
            item.IsThumbnailLoading = isLoading;
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(() => item.IsThumbnailLoading = isLoading).Task;
    }
}
