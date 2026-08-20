using FluentAssertions;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Infrastructure.Data;
using MediaVault.LinkHub.Infrastructure.Services;
using MediaVault.LinkHub.Tests.Infrastructure;

namespace MediaVault.LinkHub.Tests.Services;

public sealed class ActressServiceTests : IDisposable
{
    private readonly TestDbContextFactory _contextFactory = new();
    private readonly ActressService _sut;
    private readonly MediaVaultService _mediaVault;

    public ActressServiceTests()
    {
        _sut = new ActressService(_contextFactory);
        _mediaVault = new MediaVaultService(_contextFactory, new NullSqliteDatabaseBackupService());
    }

    [Fact]
    public async Task CreateAsync_assigns_incremental_sort_order()
    {
        var first = await _sut.CreateAsync("Alice");
        var second = await _sut.CreateAsync("Bianca");

        first.SortOrder.Should().Be(0);
        second.SortOrder.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_rejects_duplicates()
    {
        await _sut.CreateAsync("Clara");

        var act = () => _sut.CreateAsync("  Clara  ");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Clara*");
    }

    [Fact]
    public async Task FindVideosByActressIdsAsync_uses_or_semantics()
    {
        var alice = await _sut.CreateAsync("Alice");
        var bianca = await _sut.CreateAsync("Bianca");

        await using (var context = _contextFactory.CreateDbContext())
        {
            var videoAlice = new MediaFile
            {
                Path = @"C:\vault\alice.mp4",
                Name = "alice.mp4",
                Extension = ".mp4"
            };
            var videoBoth = new MediaFile
            {
                Path = @"C:\vault\both.mp4",
                Name = "both.mp4",
                Extension = ".mp4"
            };
            var videoNone = new MediaFile
            {
                Path = @"C:\vault\none.mp4",
                Name = "none.mp4",
                Extension = ".mp4"
            };

            context.MediaFiles.AddRange(videoAlice, videoBoth, videoNone);
            await context.SaveChangesAsync();
        }

        var files = await _mediaVault.GetAllAsync();
        var aliceFile = files.Single(file => file.Name == "alice.mp4");
        var bothFile = files.Single(file => file.Name == "both.mp4");

        await _mediaVault.UpdateActressesAsync(aliceFile.Id, [alice.Id]);
        await _mediaVault.UpdateActressesAsync(bothFile.Id, [alice.Id, bianca.Id]);

        var onlyBianca = await _mediaVault.FindVideosByActressIdsAsync([bianca.Id]);
        onlyBianca.Select(file => file.Name).Should().Equal("both.mp4");

        var either = await _mediaVault.FindVideosByActressIdsAsync([alice.Id, bianca.Id]);
        either.Select(file => file.Name).Should().BeEquivalentTo("alice.mp4", "both.mp4");
    }

    [Fact]
    public async Task FindVideosByFiltersAsync_and_between_actress_and_category()
    {
        var alice = await _sut.CreateAsync("Alice");
        var categoryService = new VideoCategoryService(_contextFactory);
        var action = await categoryService.CreateAsync("Acción");
        var drama = await categoryService.CreateAsync("Drama");

        await using (var context = _contextFactory.CreateDbContext())
        {
            context.MediaFiles.AddRange(
                new MediaFile { Path = @"C:\vault\a.mp4", Name = "a.mp4", Extension = ".mp4" },
                new MediaFile { Path = @"C:\vault\b.mp4", Name = "b.mp4", Extension = ".mp4" },
                new MediaFile { Path = @"C:\vault\c.mp4", Name = "c.mp4", Extension = ".mp4" });
            await context.SaveChangesAsync();
        }

        var files = await _mediaVault.GetAllAsync();
        var a = files.Single(file => file.Name == "a.mp4");
        var b = files.Single(file => file.Name == "b.mp4");
        var c = files.Single(file => file.Name == "c.mp4");

        await _mediaVault.UpdateActressesAsync(a.Id, [alice.Id]);
        await _mediaVault.UpdateCategoriesAsync(a.Id, [action.Id]);

        await _mediaVault.UpdateActressesAsync(b.Id, [alice.Id]);
        await _mediaVault.UpdateCategoriesAsync(b.Id, [drama.Id]);

        await _mediaVault.UpdateCategoriesAsync(c.Id, [action.Id]);

        var onlyCategory = await _mediaVault.FindVideosByFiltersAsync([], [action.Id], []);
        onlyCategory.Select(file => file.Name).Should().BeEquivalentTo("a.mp4", "c.mp4");

        var actressAndCategory = await _mediaVault.FindVideosByFiltersAsync([alice.Id], [action.Id], []);
        actressAndCategory.Select(file => file.Name).Should().Equal("a.mp4");
    }

    [Fact]
    public async Task FindVideosByFiltersAsync_supports_producers_or_and_and()
    {
        var producerService = new ProducerService(_contextFactory);
        var studio = await producerService.CreateAsync("Studio X");
        var other = await producerService.CreateAsync("Studio Y");
        var alice = await _sut.CreateAsync("Alice");

        await using (var context = _contextFactory.CreateDbContext())
        {
            context.MediaFiles.AddRange(
                new MediaFile { Path = @"C:\vault\p1.mp4", Name = "p1.mp4", Extension = ".mp4" },
                new MediaFile { Path = @"C:\vault\p2.mp4", Name = "p2.mp4", Extension = ".mp4" });
            await context.SaveChangesAsync();
        }

        var files = await _mediaVault.GetAllAsync();
        var p1 = files.Single(file => file.Name == "p1.mp4");
        var p2 = files.Single(file => file.Name == "p2.mp4");

        await _mediaVault.UpdateProducersAsync(p1.Id, [studio.Id]);
        await _mediaVault.UpdateActressesAsync(p1.Id, [alice.Id]);
        await _mediaVault.UpdateProducersAsync(p2.Id, [other.Id]);

        var byProducer = await _mediaVault.FindVideosByFiltersAsync([], [], [studio.Id, other.Id]);
        byProducer.Select(file => file.Name).Should().BeEquivalentTo("p1.mp4", "p2.mp4");

        var andFilter = await _mediaVault.FindVideosByFiltersAsync([alice.Id], [], [studio.Id]);
        andFilter.Select(file => file.Name).Should().Equal("p1.mp4");
    }

    [Fact]
    public async Task UpdateActressesAsync_rejects_non_video()
    {
        var actress = await _sut.CreateAsync("Dana");

        await using (var context = _contextFactory.CreateDbContext())
        {
            context.MediaFiles.Add(new MediaFile
            {
                Path = @"C:\vault\foto.jpg",
                Name = "foto.jpg",
                Extension = ".jpg"
            });
            await context.SaveChangesAsync();
        }

        var photo = (await _mediaVault.GetAllAsync()).Single();

        var act = () => _mediaVault.UpdateActressesAsync(photo.Id, [actress.Id]);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*videos*");
    }

    public void Dispose() => _contextFactory.Dispose();
}
