using MediaVault.LinkHub.Application.Services;

namespace MediaVault.LinkHub.App.Shell;

/// <summary>
/// Registra en el picker de sesión las miniaturas exclusivas persistidas por video.
/// </summary>
internal static class VideoThumbnailSessionBootstrap
{
    public static void ApplyDedicatedAssignments(
        IReadOnlyDictionary<string, IReadOnlyList<string>> assignmentsByVideoPath)
    {
        foreach (var pair in assignmentsByVideoPath)
            FolderSessionPicturePicker.RegisterDedicatedPictures(pair.Key, pair.Value);
    }

    public static async Task PrefetchWithDedicatedAsync(
        IMediaVaultService mediaVaultService,
        IEnumerable<(string ItemKey, string FolderPath)> items,
        CancellationToken cancellationToken = default)
    {
        var list = items
            .Where(item => !string.IsNullOrWhiteSpace(item.ItemKey) && !string.IsNullOrWhiteSpace(item.FolderPath))
            .ToList();

        if (list.Count == 0)
            return;

        var videoPaths = list
            .Select(item => item.ItemKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var dedicated = await mediaVaultService
            .GetThumbnailPathsByVideoPathsAsync(videoPaths, cancellationToken)
            .ConfigureAwait(false);

        ApplyDedicatedAssignments(dedicated);
        FolderSessionPicturePicker.PrefetchDistinctAssignments(list);
    }
}
