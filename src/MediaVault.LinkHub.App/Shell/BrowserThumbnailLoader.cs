using System.IO;
using System.Windows.Media;

using MediaVault.LinkHub.App.ViewModels;
using MediaVault.LinkHub.Infrastructure.Media;

namespace MediaVault.LinkHub.App.Shell;

/// <summary>
/// Carga miniaturas en segundo plano sin bloquear la navegación del explorador.
/// Videos: foto de <c>{carpeta}/Pictures</c> (distinta por archivo cuando hay varias).
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
        var list = items.ToList();
        PrefetchVideoPictures(list);
        foreach (var item in list)
            _ = LoadItemAsync(item, generation);
    }

    public void LoadItem(MediaVaultBrowserEntryItem item, int generation) =>
        _ = LoadItemAsync(item, generation);

    private static void PrefetchVideoPictures(IReadOnlyList<MediaVaultBrowserEntryItem> items)
    {
        var pairs = items
            .Where(item => !item.IsDirectory && MediaFileExtensions.IsVideo(item.FullPath))
            .Select(item =>
            {
                var folder = Path.GetDirectoryName(item.FullPath) ?? string.Empty;
                return (ItemKey: item.FullPath, FolderPath: folder);
            })
            .Where(pair => !string.IsNullOrWhiteSpace(pair.FolderPath))
            .ToList();

        if (pairs.Count > 0)
            FolderSessionPicturePicker.PrefetchDistinctAssignments(pairs);
    }

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

            if (isVideo)
            {
                thumbnail = await LoadVideoPictureThumbnailAsync(item.FullPath).ConfigureAwait(false);

                // Fallback breve al Shell solo si no hay fotos de actriz.
                if (thumbnail is null)
                {
                    thumbnail = await WindowsShellThumbnailProvider
                        .GetThumbnailAsync(item.FullPath, false)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                thumbnail = await WindowsShellThumbnailProvider
                    .GetThumbnailAsync(item.FullPath, false)
                    .ConfigureAwait(false);
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

    private static Task<ImageSource?> LoadVideoPictureThumbnailAsync(string videoPath)
    {
        var folderPath = Path.GetDirectoryName(videoPath);
        if (string.IsNullOrWhiteSpace(folderPath))
            return Task.FromResult<ImageSource?>(null);

        return Task.Run(() =>
            FolderSessionPicturePicker.TryLoadThumbnailForItem(folderPath, videoPath, 128));
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
