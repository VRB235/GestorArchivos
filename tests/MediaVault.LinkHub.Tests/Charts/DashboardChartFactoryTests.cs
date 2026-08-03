using System.Collections.ObjectModel;
using FluentAssertions;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using MediaVault.LinkHub.App.Charts;
using MediaVault.LinkHub.App.ViewModels;
using MediaVault.LinkHub.Application.Models.Dashboard;

namespace MediaVault.LinkHub.Tests.Charts;

public sealed class DashboardChartFactoryTests
{
    [Fact]
    public void CreateDrawMarginFrame_returns_configured_frame()
    {
        var frame = DashboardChartFactory.CreateDrawMarginFrame();

        frame.Fill.Should().NotBeNull();
        frame.Stroke.Should().NotBeNull();
    }

    [Fact]
    public void PopulateCategoryPieChart_ignores_zero_counts_and_builds_series()
    {
        var series = new ObservableCollection<ISeries>();
        var items = new[]
        {
            new CategoryDistributionItem { Categoria = Domain.Enums.LinkCategory.Oficial, Count = 3 },
            new CategoryDistributionItem { Categoria = Domain.Enums.LinkCategory.Gratis, Count = 0 },
            new CategoryDistributionItem { Categoria = Domain.Enums.LinkCategory.Descarga, Count = 1 }
        };

        DashboardChartFactory.PopulateCategoryPieChart(items, series);

        series.Should().HaveCount(2);
        series.Should().AllBeOfType<PieSeries<int>>();
        series.Cast<PieSeries<int>>().Select(item => item.Name).Should().BeEquivalentTo(["Oficial", "Descarga"]);
    }

    [Fact]
    public void PopulateTopViewsChart_with_empty_items_clears_series_and_configures_axes()
    {
        var panel = CreatePanel();

        DashboardChartFactory.PopulateTopViewsChart([], panel);

        panel.Series.Should().BeEmpty();
        panel.XAxes.Should().ContainSingle(axis => axis.MinLimit == 0);
        panel.YAxes.Should().BeEmpty();
    }

    [Fact]
    public void PopulateTopViewsChart_maps_view_counts_to_row_series()
    {
        var panel = CreatePanel();
        var items = new[]
        {
            CreateStats("alpha.mp4", vecesAbierto: 8),
            CreateStats("beta.jpg", vecesAbierto: 3)
        };

        DashboardChartFactory.PopulateTopViewsChart(items, panel);

        panel.Series.Should().ContainSingle();
        var rowSeries = panel.Series[0].Should().BeOfType<RowSeries<int>>().Subject;
        rowSeries.Values.Should().Equal(8, 3);
        panel.YAxes.Should().ContainSingle(axis => axis.Labels!.Count == 2);
    }

    [Fact]
    public void PopulateTopRankedChart_maps_rankings_to_row_series()
    {
        var panel = CreatePanel();
        var items = new[]
        {
            CreateStats("top.mp4", rankingGlobal: 4.5),
            CreateStats("second.jpg", rankingGlobal: 3.0)
        };

        DashboardChartFactory.PopulateTopRankedChart(items, panel);

        var rowSeries = panel.Series[0].Should().BeOfType<RowSeries<double>>().Subject;
        rowSeries.Values.Should().Equal(4.5, 3.0);
        panel.XAxes.Should().ContainSingle(axis => axis.MaxLimit == 5);
    }

    [Fact]
    public void PopulateCategoryRankingChart_with_items_builds_column_series()
    {
        var series = new ObservableCollection<ISeries>();
        var items = new[]
        {
            new MediaCategoryRankingItem { CategoryName = "Acción", AverageRanking = 4.2, VideoCount = 2 },
            new MediaCategoryRankingItem { CategoryName = "Comedia muy larga para truncar", AverageRanking = 3.1, VideoCount = 1 }
        };

        DashboardChartFactory.PopulateCategoryRankingChart(items, series, out var xAxes, out var yAxes);

        series.Should().ContainSingle().Which.Should().BeOfType<ColumnSeries<double>>();
        xAxes.Should().ContainSingle(axis =>
            axis.Labels!.Count == 2
            && axis.Labels![1].EndsWith("..."));
        yAxes.Should().ContainSingle(axis => axis.MaxLimit == 5);
    }

    [Fact]
    public void PopulateRankingChart_builds_single_average_column()
    {
        var series = new ObservableCollection<ISeries>();

        DashboardChartFactory.PopulateRankingChart(3.67, series, out var xAxes, out var yAxes);

        var columnSeries = series.Should().ContainSingle().Which.Should().BeOfType<ColumnSeries<double>>().Subject;
        columnSeries.Values.Should().Equal(3.67);
        xAxes.Should().ContainSingle(axis => axis.Labels!.Single() == "Ranking global");
        yAxes.Should().ContainSingle(axis => axis.MaxLimit == 5);
    }

    [Fact]
    public void PopulateMediaDistributionPieChart_builds_one_slice_per_label()
    {
        var series = new ObservableCollection<ISeries>();
        var items = new[]
        {
            new MediaDistributionItem { Label = "Acción", Count = 4 },
            new MediaDistributionItem { Label = "Sin categoría", Count = 2 }
        };

        DashboardChartFactory.PopulateMediaDistributionPieChart(items, series);

        series.Should().HaveCount(2);
        series.Cast<PieSeries<int>>().Select(item => item.Name).Should().BeEquivalentTo(["Acción", "Sin categoría"]);
    }

    private static DashboardChartPanelViewModel CreatePanel() =>
        new()
        {
            Title = "Panel de prueba",
            DrawMarginFrame = DashboardChartFactory.CreateDrawMarginFrame()
        };

    private static MediaFileViewStats CreateStats(
        string name,
        int vecesAbierto = 0,
        double rankingGlobal = 0) =>
        new()
        {
            Id = Random.Shared.Next(1, 10_000),
            Name = name,
            Path = $@"C:\vault\{name}",
            Extension = Path.GetExtension(name),
            VecesAbierto = vecesAbierto,
            RankingGlobal = rankingGlobal,
            IsVideo = name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
        };
}
