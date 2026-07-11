using FluentAssertions;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Domain.Enums;
using MediaVault.LinkHub.Infrastructure.Data;
using MediaVault.LinkHub.Infrastructure.Services;
using MediaVault.LinkHub.Tests.Infrastructure;

namespace MediaVault.LinkHub.Tests.Services;

public sealed class DashboardServiceTests : IDisposable
{
    private readonly TestDbContextFactory _contextFactory = new();
    private readonly DashboardService _sut;

    public DashboardServiceTests() =>
        _sut = new DashboardService(_contextFactory);

    [Fact]
    public async Task GetStatisticsAsync_builds_top10_by_views_and_link_distribution()
    {
        await SeedDashboardDataAsync();

        var stats = await _sut.GetStatisticsAsync();

        stats.Top10MostViewed.Select(file => file.Name).Should().ContainInOrder(
            "popular.mp4",
            "medio.mp4",
            "foto.jpg");

        stats.LinkDistributionByCategory.Should().Contain(item =>
            item.Categoria == LinkCategory.Oficial && item.Count == 2);
        stats.LinkDistributionByCategory.Should().Contain(item =>
            item.Categoria == LinkCategory.Gratis && item.Count == 1);
        stats.TotalWebLinks.Should().Be(3);
        stats.TotalQuickNotes.Should().Be(1);
    }

    [Fact]
    public async Task GetStatisticsAsync_computes_average_ranking_only_for_ranked_files()
    {
        await SeedDashboardDataAsync();

        var stats = await _sut.GetStatisticsAsync();

        stats.AverageGlobalRanking.Should().BeApproximately(4.0, 0.01);
        stats.Top10BestRankedVideos.Should().ContainSingle(file => file.Name == "popular.mp4");
        stats.AverageRankingByVideoCategory.Should().Contain(item =>
            item.CategoryName == "Acción" && item.AverageRanking > 0);
        stats.VideoDistributionByCategory.Should().Contain(item =>
            item.Label == "Acción" && item.Count == 1);
        stats.VideoDistributionByCategory.Should().Contain(item =>
            item.Label == "Sin categoría" && item.Count == 2);
    }

    [Fact]
    public async Task GetTop10MostViewedAsync_delegates_to_statistics()
    {
        await SeedDashboardDataAsync();

        var top = await _sut.GetTop10MostViewedAsync();

        top.Should().HaveCount(3);
        top[0].VecesAbierto.Should().Be(12);
    }

    private async Task SeedDashboardDataAsync()
    {
        await using var context = _contextFactory.CreateDbContext();

        context.QuickNotes.Add(new QuickNote
        {
            Contenido = "Nota dashboard",
            FechaCreacion = DateTime.UtcNow
        });

        context.WebLinks.AddRange(
            new WebLink { Nombre = "A", Url = "https://a.test", Categoria = LinkCategory.Oficial, FechaCreacion = DateTime.UtcNow },
            new WebLink { Nombre = "B", Url = "https://b.test", Categoria = LinkCategory.Oficial, FechaCreacion = DateTime.UtcNow },
            new WebLink { Nombre = "C", Url = "https://c.test", Categoria = LinkCategory.Gratis, FechaCreacion = DateTime.UtcNow });

        var actionCategory = new VideoCategory { Name = "Acción", SortOrder = 0 };

        var popularVideo = new MediaFile
        {
            Path = @"C:\vault\popular.mp4",
            Name = "popular.mp4",
            Extension = ".mp4",
            VecesAbierto = 12,
            RankingCalidad = 5,
            RankingContenido = 4,
            RankingGusto = 3
        };
        popularVideo.Categories.Add(actionCategory);

        context.MediaFiles.AddRange(
            popularVideo,
            new MediaFile
            {
                Path = @"C:\vault\foto.jpg",
                Name = "foto.jpg",
                Extension = ".jpg",
                VecesAbierto = 5
            },
            new MediaFile
            {
                Path = @"C:\vault\medio.mp4",
                Name = "medio.mp4",
                Extension = ".mp4",
                VecesAbierto = 7
            });

        await context.SaveChangesAsync();
    }

    public void Dispose() => _contextFactory.Dispose();
}
