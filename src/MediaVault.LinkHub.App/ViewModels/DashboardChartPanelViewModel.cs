using System.Collections.ObjectModel;
using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;

using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing;

using MediaVault.LinkHub.App.Charts;
using MediaVault.LinkHub.App.Shell;
using MediaVault.LinkHub.Application.Models.Dashboard;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class DashboardChartPanelViewModel : ObservableObject
{
    private int _hoverPreviewGeneration;
    private DashboardChartPreviewMode _previewMode = DashboardChartPreviewMode.None;

    public string Title { get; init; } = string.Empty;

    public string EmptyMessage { get; init; } = string.Empty;

    public bool IsPie { get; init; }

    /// <summary>Si el expander de esta sección inicia expandido.</summary>
    public bool IsExpanded { get; init; }

    [ObservableProperty]
    private bool _hasData;

    public ObservableCollection<ISeries> Series { get; } = [];

    [ObservableProperty]
    private Axis[] _xAxes = [];

    [ObservableProperty]
    private Axis[] _yAxes = [];

    public DrawMarginFrame DrawMarginFrame { get; init; } = null!;

    public MediaFileViewStats[] MediaFiles { get; set; } = [];

    public int[] MediaFileIds => MediaFiles.Select(file => file.Id).ToArray();

    public bool SupportsFileOpen => MediaFiles.Length > 0;

    public string FileOpenHint => SupportsFileOpen
        ? "Pase el mouse sobre una barra para ver la vista previa. Clic para abrir el archivo."
        : string.Empty;

    [ObservableProperty]
    private ImageSource? _hoverPreview;

    [ObservableProperty]
    private bool _showHoverPreview;

    [ObservableProperty]
    private string _hoverPreviewCaption = string.Empty;

    public void SetPreviewMode(DashboardChartPreviewMode mode) =>
        _previewMode = mode;

    public async Task ShowHoverPreviewAsync(int index)
    {
        if (_previewMode == DashboardChartPreviewMode.None
            || index < 0
            || index >= MediaFiles.Length)
        {
            ClearHoverPreview();
            return;
        }

        var file = MediaFiles[index];
        var generation = ++_hoverPreviewGeneration;

        ImageSource? thumbnail = null;
        var folderPath = System.IO.Path.GetDirectoryName(file.Path);
        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            thumbnail = await Task.Run(() =>
                FolderSessionPicturePicker.TryLoadSessionThumbnail(folderPath, 160)).ConfigureAwait(true);
        }

        thumbnail ??= await WindowsShellThumbnailProvider
            .GetThumbnailAsync(file.Path, isDirectory: false, 160)
            .ConfigureAwait(true);

        if (generation != _hoverPreviewGeneration)
            return;

        HoverPreview = thumbnail;
        HoverPreviewCaption = _previewMode switch
        {
            DashboardChartPreviewMode.Views => $"{file.VecesAbierto} aperturas",
            DashboardChartPreviewMode.Ranking => $"{file.RankingGlobal:F2} / 5",
            _ => string.Empty
        };
        ShowHoverPreview = thumbnail is not null;
    }

    public void ClearHoverPreview()
    {
        _hoverPreviewGeneration++;
        ShowHoverPreview = false;
        HoverPreview = null;
        HoverPreviewCaption = string.Empty;
    }
}
