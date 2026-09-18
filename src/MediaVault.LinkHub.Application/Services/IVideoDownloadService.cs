using MediaVault.LinkHub.Application.Models.Downloads;
using MediaVault.LinkHub.Domain.Entities;

namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// Cola de descargas HLS/.m3u8 (y URLs de segmento) con progreso y apertura local.
/// </summary>
public interface IVideoDownloadService
{
    event EventHandler<VideoDownloadProgressUpdate>? ProgressChanged;

    Task<IReadOnlyList<VideoDownload>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<VideoDownload> EnqueueAsync(string sourceUrl, string? displayName = null, CancellationToken cancellationToken = default);

    Task RetryAsync(int id, CancellationToken cancellationToken = default);

    Task CancelAsync(int id, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, bool deleteFile = true, CancellationToken cancellationToken = default);

    Task RecoverInterruptedAsync(CancellationToken cancellationToken = default);

    bool TryOpen(int id);
}
