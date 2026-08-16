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
    private readonly string _mediaRoot;

    public DashboardServiceTests()
    {
        _sut = new DashboardService(_contextFactory);
        _mediaRoot = Path.Combine(Path.GetTempPath(), "MediaVaultLinkHubTests", "dashboard", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_mediaRoot);
    }

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
    public async Task GetStatisticsAsync_computes_video_open_and_unrated_metrics()
    {
        await SeedDashboardDataAsync();

        var stats = await _sut.GetStatisticsAsync();

        stats.TotalVideos.Should().Be(2);
        stats.TotalVideoOpens.Should().Be(19);
        stats.VideosNeverOpened.Should().Be(0);
        stats.VideosUnrated.Should().Be(1);
        stats.Top10MostViewedVideos.Select(file => file.Name).Should().ContainInOrder(
            "popular.mp4",
            "medio.mp4");
    }

    [Fact]
    public async Task GetVideoRecommendationsAsync_excludes_requested_ids_when_possible()
    {
        await SeedDashboardDataAsync();

        var firstBatch = await _sut.GetVideoRecommendationsAsync(count: 1);
        firstBatch.Should().ContainSingle();

        var secondBatch = await _sut.GetVideoRecommendationsAsync(
            excludeMediaFileIds: [firstBatch[0].Id],
            count: 1);
        secondBatch.Should().ContainSingle();
        secondBatch[0].Id.Should().NotBe(firstBatch[0].Id);
        secondBatch[0].IsVideo.Should().BeTrue();
    }

    [Fact]
    public async Task GetVideoRecommendationsAsync_skips_videos_missing_on_disk()
    {
        var existing = CreateTempMedia("exists.mp4");
        await using (var context = _contextFactory.CreateDbContext())
        {
            context.MediaFiles.AddRange(
                new MediaFile
                {
                    Path = existing,
                    Name = "exists.mp4",
                    Extension = ".mp4",
                    RankingCalidad = 5,
                    RankingContenido = 5,
                    RankingGusto = 5
                },
                new MediaFile
                {
                    Path = Path.Combine(_mediaRoot, "missing-file.mp4"),
                    Name = "missing-file.mp4",
                    Extension = ".mp4",
                    RankingCalidad = 5,
                    RankingContenido = 5,
                    RankingGusto = 5
                });
            await context.SaveChangesAsync();
        }

        for (var i = 0; i < 8; i++)
        {
            var recommendations = await _sut.GetVideoRecommendationsAsync(count: 5);
            recommendations.Should().ContainSingle();
            recommendations[0].Name.Should().Be("exists.mp4");
            recommendations[0].Path.Should().Be(existing);
        }
    }

    [Fact]
    public async Task GetTop10MostViewedAsync_delegates_to_statistics()
    {
        await SeedDashboardDataAsync();

        var top = await _sut.GetTop10MostViewedAsync();

        top.Should().HaveCount(3);
        top[0].VecesAbierto.Should().Be(12);
    }

    [Fact]
    public async Task GetRankedVideoRecommendationsAsync_skips_excluded_and_only_returns_ranked()
    {
        await using var context = _contextFactory.CreateDbContext();
        context.MediaFiles.AddRange(
            new MediaFile
            {
                Path = CreateTempMedia("five.mp4"),
                Name = "five.mp4",
                Extension = ".mp4",
                RankingCalidad = 5,
                RankingContenido = 5,
                RankingGusto = 5
            },
            new MediaFile
            {
                Path = CreateTempMedia("four.mp4"),
                Name = "four.mp4",
                Extension = ".mp4",
                RankingCalidad = 4,
                RankingContenido = 4,
                RankingGusto = 4
            },
            new MediaFile
            {
                Path = CreateTempMedia("unrated.mp4"),
                Name = "unrated.mp4",
                Extension = ".mp4"
            });
        await context.SaveChangesAsync();

        var firstBatch = await _sut.GetRankedVideoRecommendationsAsync([], count: 1);
        firstBatch.Should().ContainSingle();
        firstBatch[0].Name.Should().Be("five.mp4");

        var nextBatch = await _sut.GetRankedVideoRecommendationsAsync([firstBatch[0].Id], count: 1);
        nextBatch.Should().ContainSingle();
        nextBatch[0].Name.Should().Be("four.mp4");

        var none = await _sut.GetRankedVideoRecommendationsAsync(
            [firstBatch[0].Id, nextBatch[0].Id],
            count: 5);
        // Tras agotar calificados, rellena con el no calificado restante.
        none.Should().ContainSingle(file => file.Name == "unrated.mp4");
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
            Path = CreateTempMedia("popular.mp4"),
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
                Path = CreateTempMedia("foto.jpg"),
                Name = "foto.jpg",
                Extension = ".jpg",
                VecesAbierto = 5
            },
            new MediaFile
            {
                Path = CreateTempMedia("medio.mp4"),
                Name = "medio.mp4",
                Extension = ".mp4",
                VecesAbierto = 7
            });

        await context.SaveChangesAsync();
    }

    private string CreateTempMedia(string fileName)
    {
        var path = Path.Combine(_mediaRoot, fileName);
        File.WriteAllBytes(path, [0x00, 0x01, 0x02, 0x03]);
        return path;
    }

    public void Dispose()
    {
        _contextFactory.Dispose();
        try
        {
            if (Directory.Exists(_mediaRoot))
                Directory.Delete(_mediaRoot, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup in tests.
        }
    }
}
