using System.IO;

using CommunityToolkit.Mvvm.ComponentModel;

using MediaVault.LinkHub.Application.Models.Downloads;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Domain.Enums;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class VideoDownloadListItem : ObservableObject
{
    public VideoDownloadListItem(VideoDownload entity)
    {
        Id = entity.Id;
        SourceUrl = entity.SourceUrl;
        DisplayName = entity.DisplayName;
        CreatedAt = entity.CreatedAt;
        Apply(entity);
    }

    public int Id { get; }

    public string SourceUrl { get; }

    public string DisplayName { get; }

    public DateTime CreatedAt { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(CanOpen))]
    [NotifyPropertyChangedFor(nameof(CanRetry))]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private VideoDownloadStatus _status;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private int _progressPercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private int _segmentsTotal;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    private int _segmentsCompleted;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOpen))]
    private string? _localPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string? _errorMessage;

    [ObservableProperty]
    private DateTime? _completedAt;

    public bool CanOpen =>
        Status == VideoDownloadStatus.Completed
        && !string.IsNullOrWhiteSpace(LocalPath)
        && File.Exists(LocalPath);

    public bool CanRetry =>
        Status is VideoDownloadStatus.Failed or VideoDownloadStatus.Cancelled;

    public bool CanCancel =>
        Status is VideoDownloadStatus.Pending or VideoDownloadStatus.Downloading;

    public string StatusText => Status switch
    {
        VideoDownloadStatus.Pending => "En cola",
        VideoDownloadStatus.Downloading => "Descargando",
        VideoDownloadStatus.Completed => "Completada",
        VideoDownloadStatus.Failed => string.IsNullOrWhiteSpace(ErrorMessage) ? "Error" : $"Error: {ErrorMessage}",
        VideoDownloadStatus.Cancelled => "Cancelada",
        _ => Status.ToString()
    };

    public string ProgressText =>
        SegmentsTotal > 0
            ? $"{ProgressPercent}% ({SegmentsCompleted}/{SegmentsTotal} segmentos)"
            : $"{ProgressPercent}%";

    public void Apply(VideoDownload entity)
    {
        Status = entity.Status;
        ProgressPercent = entity.ProgressPercent;
        SegmentsTotal = entity.SegmentsTotal;
        SegmentsCompleted = entity.SegmentsCompleted;
        LocalPath = entity.LocalPath;
        ErrorMessage = entity.ErrorMessage;
        CompletedAt = entity.CompletedAt;
    }

    public void Apply(VideoDownloadProgressUpdate update)
    {
        Status = update.Status;
        ProgressPercent = update.ProgressPercent;
        SegmentsTotal = update.SegmentsTotal;
        SegmentsCompleted = update.SegmentsCompleted;
        LocalPath = update.LocalPath;
        ErrorMessage = update.ErrorMessage;
        CompletedAt = update.CompletedAt;
    }
}
