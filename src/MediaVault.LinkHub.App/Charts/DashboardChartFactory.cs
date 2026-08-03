using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Drawing;
using LiveChartsCore.SkiaSharpView.Painting;
using MediaVault.LinkHub.Application.Models.Dashboard;
using MediaVault.LinkHub.App.ViewModels;
using SkiaSharp;

namespace MediaVault.LinkHub.App.Charts;

internal static class DashboardChartFactory
{
    private static readonly SKColor AccentBlue = SKColor.Parse("#3B82F6");
    private static readonly SKColor AccentPurple = SKColor.Parse("#8B5CF6");
    private static readonly SKColor AccentGreen = SKColor.Parse("#10B981");
    private static readonly SKColor AccentAmber = SKColor.Parse("#F59E0B");
    private static readonly SKColor AccentRose = SKColor.Parse("#F43F5E");
    private static readonly SKColor TextPrimary = SKColor.Parse("#F3F4F6");
    private static readonly SKColor TextSecondary = SKColor.Parse("#9CA3AF");
    private static readonly SKColor GridLine = SKColor.Parse("#2D3348");
    private static readonly SKColor PlotBackground = SKColor.Parse("#12151E");

    private static readonly string[] CategoryPalette =
    [
        "#3B82F6",
        "#8B5CF6",
        "#10B981",
        "#F59E0B",
        "#EF4444",
        "#F43F5E",
        "#06B6D4",
        "#84CC16"
    ];

    public static DrawMarginFrame CreateDrawMarginFrame() =>
        new()
        {
            Fill = new SolidColorPaint(PlotBackground),
            Stroke = new SolidColorPaint(GridLine) { StrokeThickness = 1 }
        };

    public static void PopulateTopViewsChart(
        IReadOnlyList<MediaFileViewStats> items,
        DashboardChartPanelViewModel panel,
        SKColor? barColor = null,
        Action<int>? onPointClicked = null)
    {
        panel.SetPreviewMode(DashboardChartPreviewMode.Views);
        PopulateHorizontalIntBarChart(
            items,
            panel.Series,
            out var yAxes,
            out var xAxes,
            item => item.VecesAbierto,
            barColor ?? AccentBlue,
            panel,
            onPointClicked);
        panel.YAxes = yAxes;
        panel.XAxes = xAxes;
    }

    public static void PopulateTopRankedChart(
        IReadOnlyList<MediaFileViewStats> items,
        DashboardChartPanelViewModel panel,
        SKColor? barColor = null,
        Action<int>? onPointClicked = null)
    {
        panel.SetPreviewMode(DashboardChartPreviewMode.Ranking);
        PopulateHorizontalDoubleBarChart(
            items,
            panel.Series,
            out var yAxes,
            out var xAxes,
            item => item.RankingGlobal,
            value => $"{value:F2}",
            barColor ?? AccentPurple,
            panel,
            onPointClicked);
        panel.YAxes = yAxes;
        panel.XAxes = xAxes;
    }

    public static void PopulateCategoryPieChart(
        IReadOnlyList<CategoryDistributionItem> items,
        ObservableCollection<ISeries> series) =>
        PopulateDistributionPieChart(
            items.Where(entry => entry.Count > 0).Select(entry => (entry.Label, entry.Count)),
            series);

    public static void PopulateMediaDistributionPieChart(
        IReadOnlyList<MediaDistributionItem> items,
        ObservableCollection<ISeries> series) =>
        PopulateDistributionPieChart(
            items.Where(entry => entry.Count > 0).Select(entry => (entry.Label, entry.Count)),
            series);

    public static void PopulateCategoryRankingChart(
        IReadOnlyList<MediaCategoryRankingItem> items,
        ObservableCollection<ISeries> series,
        out Axis[] xAxes,
        out Axis[] yAxes)
    {
        series.Clear();

        if (items.Count == 0)
        {
            xAxes = [];
            yAxes = [CreateValueAxis(maxLimit: 5)];
            return;
        }

        series.Add(new ColumnSeries<double>
        {
            Values = items.Select(item => item.AverageRanking).ToArray(),
            Name = "Promedio",
            Fill = new SolidColorPaint(AccentGreen),
            DataLabelsPaint = new SolidColorPaint(TextPrimary),
            DataLabelsFormatter = point => $"{point.Model:F2}",
            DataLabelsSize = 12,
            MaxBarWidth = 48
        });

        xAxes =
        [
            new Axis
            {
                Labels = items.Select(item => TruncateName(item.CategoryName, 16)).ToArray(),
                LabelsPaint = new SolidColorPaint(TextSecondary),
                SeparatorsPaint = new SolidColorPaint(GridLine),
                TextSize = 11
            }
        ];

        yAxes = [CreateValueAxis(maxLimit: 5)];
    }

    public static void PopulateRankingChart(
        double averageGlobalRanking,
        ObservableCollection<ISeries> series,
        out Axis[] xAxes,
        out Axis[] yAxes)
    {
        series.Clear();

        series.Add(new ColumnSeries<double>
        {
            Values = [averageGlobalRanking],
            Name = "Promedio",
            Fill = new SolidColorPaint(AccentPurple),
            DataLabelsPaint = new SolidColorPaint(TextPrimary),
            DataLabelsFormatter = point => $"{point.Model:F2}",
            DataLabelsSize = 14,
            MaxBarWidth = 80
        });

        xAxes =
        [
            new Axis
            {
                Labels = ["Ranking global"],
                LabelsPaint = new SolidColorPaint(TextSecondary),
                SeparatorsPaint = new SolidColorPaint(GridLine),
                TextSize = 12
            }
        ];

        yAxes = [CreateValueAxis(maxLimit: 5)];
    }

    private static void PopulateHorizontalIntBarChart(
        IReadOnlyList<MediaFileViewStats> items,
        ObservableCollection<ISeries> series,
        out Axis[] yAxes,
        out Axis[] xAxes,
        Func<MediaFileViewStats, int> valueSelector,
        SKColor barColor,
        DashboardChartPanelViewModel? panel,
        Action<int>? onPointClicked = null)
    {
        series.Clear();

        if (items.Count == 0)
        {
            yAxes = [];
            xAxes = [CreateValueAxis()];
            return;
        }

        var rowSeries = new RowSeries<int>
        {
            Values = items.Select(valueSelector).ToArray(),
            Name = "Valor",
            Fill = new SolidColorPaint(barColor),
            DataLabelsPaint = new SolidColorPaint(TextPrimary),
            DataLabelsFormatter = point => $"{point.Model}",
            DataLabelsSize = 12
        };

        AttachBarInteractions(rowSeries, panel, onPointClicked);
        series.Add(rowSeries);

        yAxes =
        [
            new Axis
            {
                Labels = items.Select((_, index) => $"{index + 1}").ToArray(),
                LabelsPaint = new SolidColorPaint(TextSecondary),
                SeparatorsPaint = new SolidColorPaint(GridLine),
                TextSize = 11
            }
        ];

        xAxes = [CreateValueAxis()];
    }

    private static void AttachBarInteractions<T>(
        RowSeries<T> series,
        DashboardChartPanelViewModel? panel,
        Action<int>? onPointClicked)
    {
        if (panel is not null)
        {
            series.ChartPointPointerHover += (chart, point) =>
            {
                if (point?.Visual is null)
                {
                    panel.ClearHoverPreview();
                    return;
                }

                panel.ShowHoverPreviewAsync(point.Index);
            };

            series.ChartPointPointerHoverLost += (_, _) =>
                panel.ClearHoverPreview();
        }

        if (onPointClicked is null)
            return;

        series.ChartPointPointerDown += (_, point) =>
        {
            if (point?.Visual is null)
                return;

            onPointClicked(point.Index);
        };
    }

    private static void PopulateHorizontalDoubleBarChart(
        IReadOnlyList<MediaFileViewStats> items,
        ObservableCollection<ISeries> series,
        out Axis[] yAxes,
        out Axis[] xAxes,
        Func<MediaFileViewStats, double> valueSelector,
        Func<double, string> labelFormatter,
        SKColor barColor,
        DashboardChartPanelViewModel? panel,
        Action<int>? onPointClicked = null)
    {
        series.Clear();

        if (items.Count == 0)
        {
            yAxes = [];
            xAxes = [CreateValueAxis(maxLimit: 5)];
            return;
        }

        var rowSeries = new RowSeries<double>
        {
            Values = items.Select(valueSelector).ToArray(),
            Name = "Ranking",
            Fill = new SolidColorPaint(barColor),
            DataLabelsPaint = new SolidColorPaint(TextPrimary),
            DataLabelsFormatter = point => labelFormatter(point.Model),
            DataLabelsSize = 12
        };

        AttachBarInteractions(rowSeries, panel, onPointClicked);
        series.Add(rowSeries);

        yAxes =
        [
            new Axis
            {
                Labels = items.Select((_, index) => $"{index + 1}").ToArray(),
                LabelsPaint = new SolidColorPaint(TextSecondary),
                SeparatorsPaint = new SolidColorPaint(GridLine),
                TextSize = 11
            }
        ];

        xAxes = [CreateValueAxis(maxLimit: 5)];
    }

    private static void PopulateDistributionPieChart(
        IEnumerable<(string Label, int Count)> items,
        ObservableCollection<ISeries> series)
    {
        series.Clear();

        var index = 0;
        foreach (var item in items)
        {
            series.Add(new PieSeries<int>
            {
                Name = item.Label,
                Values = [item.Count],
                Fill = new SolidColorPaint(SKColor.Parse(CategoryPalette[index % CategoryPalette.Length])),
                DataLabelsPaint = new SolidColorPaint(TextPrimary),
                DataLabelsFormatter = point => $"{item.Label}: {point.Model}",
                DataLabelsSize = 11,
                ToolTipLabelFormatter = point => $"{item.Label}: {point.Model}"
            });

            index++;
        }
    }

    private static Axis CreateValueAxis(double? maxLimit = null) =>
        new()
        {
            MinLimit = 0,
            MaxLimit = maxLimit,
            LabelsPaint = new SolidColorPaint(TextSecondary),
            SeparatorsPaint = new SolidColorPaint(GridLine),
            TextSize = 12
        };

    private static string TruncateName(string name, int maxLength = 24) =>
        name.Length <= maxLength ? name : $"{name[..(maxLength - 3)]}...";
}
