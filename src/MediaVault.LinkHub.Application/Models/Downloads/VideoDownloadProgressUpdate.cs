using MediaVault.LinkHub.Domain.Enums;

namespace MediaVault.LinkHub.Application.Models.Downloads;

public sealed class VideoDownloadProgressUpdate
{
    public required int Id { get; init; }

    public required VideoDownloadStatus Status { get; init; }

    public int ProgressPercent { get; init; }

    public int SegmentsTotal { get; init; }

    public int SegmentsCompleted { get; init; }

    public string? LocalPath { get; init; }

    public string? ErrorMessage { get; init; }

    public DateTime? CompletedAt { get; init; }
}
