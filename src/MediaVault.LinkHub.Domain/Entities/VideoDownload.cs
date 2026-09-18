using MediaVault.LinkHub.Domain.Common;
using MediaVault.LinkHub.Domain.Enums;

namespace MediaVault.LinkHub.Domain.Entities;

/// <summary>
/// Descarga de video (HLS/.m3u8 u URL directa) con estado y ruta local.
/// </summary>
public class VideoDownload : EntityBase
{
    public string SourceUrl { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public VideoDownloadStatus Status { get; set; } = VideoDownloadStatus.Pending;

    public int ProgressPercent { get; set; }

    public int SegmentsTotal { get; set; }

    public int SegmentsCompleted { get; set; }

    public string? LocalPath { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}
