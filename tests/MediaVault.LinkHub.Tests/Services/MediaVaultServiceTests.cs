using FluentAssertions;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Infrastructure.Data;
using MediaVault.LinkHub.Infrastructure.Services;
using MediaVault.LinkHub.Tests.Infrastructure;

namespace MediaVault.LinkHub.Tests.Services;

public sealed class MediaVaultServiceTests : IDisposable
{
    private readonly TestDbContextFactory _contextFactory = new();
    private readonly MediaVaultService _sut;
    private readonly string _rootDirectory;

    public MediaVaultServiceTests()
    {
        _sut = new MediaVaultService(_contextFactory);
        _rootDirectory = Path.Combine(Path.GetTempPath(), "MediaVaultTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDirectory);
    }

    [Fact]
    public async Task IndexDirectoryAsync_indexes_supported_files_and_skips_unsupported()
    {
        await File.WriteAllTextAsync(Path.Combine(_rootDirectory, "clip.mp4"), "video");
        await File.WriteAllTextAsync(Path.Combine(_rootDirectory, "readme.txt"), "text");

        var result = await _sut.IndexDirectoryAsync(_rootDirectory);

        result.FilesAdded.Should().Be(1);
        result.FilesIndexed.Should().Be(1);
        result.FilesSkipped.Should().Be(1);

        var indexed = await _sut.GetAllAsync();
        indexed.Should().ContainSingle(file => file.Name == "clip.mp4");
    }

    [Fact]
    public async Task UpdateRankingsAsync_persists_values_within_scale()
    {
        var indexed = await SeedIndexedFileAsync("ranked.jpg");

        var updated = await _sut.UpdateRankingsAsync(indexed.Id, 5, 4, 3);

        updated.RankingCalidad.Should().Be(5);
        updated.RankingContenido.Should().Be(4);
        updated.RankingGusto.Should().Be(3);
    }

    [Fact]
    public async Task UpdateRankingsAsync_rejects_values_outside_star_scale()
    {
        var indexed = await SeedIndexedFileAsync("invalid.jpg");

        var act = () => _sut.UpdateRankingsAsync(indexed.Id, 6, 0, 0);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ListDirectoryEntriesAsync_rejects_paths_outside_index_root()
    {
        var outsidePath = Path.Combine(Path.GetTempPath(), "MediaVaultOutside", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsidePath);

        var act = () => _sut.ListDirectoryEntriesAsync(outsidePath, _rootDirectory);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*fuera del directorio*");
    }

    [Fact]
    public async Task ListDirectoryEntriesAsync_merges_indexed_metadata_for_files()
    {
        var filePath = Path.Combine(_rootDirectory, "merged.mp4");
        await File.WriteAllTextAsync(filePath, "video");

        var indexed = await SeedIndexedFileAsync("merged.mp4", filePath, vecesAbierto: 3);

        var entries = await _sut.ListDirectoryEntriesAsync(_rootDirectory, _rootDirectory);

        entries.Should().Contain(entry =>
            !entry.IsDirectory
            && entry.Name == "merged.mp4"
            && entry.MediaFile != null
            && entry.MediaFile.Id == indexed.Id
            && entry.MediaFile.VecesAbierto == 3);
    }

    [Fact]
    public async Task ListDirectoryEntriesAsync_matches_indexed_path_ignoring_case()
    {
        var filePath = Path.Combine(_rootDirectory, "CaseClip.mp4");
        await File.WriteAllTextAsync(filePath, "video");

        var storedPath = filePath.ToLowerInvariant();
        await SeedIndexedFileAsync("CaseClip.mp4", storedPath, vecesAbierto: 2);

        var entries = await _sut.ListDirectoryEntriesAsync(_rootDirectory, _rootDirectory);

        entries.Should().Contain(entry =>
            !entry.IsDirectory
            && entry.Name == "CaseClip.mp4"
            && entry.MediaFile != null
            && entry.MediaFile.VecesAbierto == 2);
    }

    [Fact]
    public async Task EnsureIndexedAsync_creates_missing_media_file_record()
    {
        var filePath = Path.Combine(_rootDirectory, "new-index.mp4");
        await File.WriteAllTextAsync(filePath, "video");

        var created = await _sut.EnsureIndexedAsync(filePath);

        created.Id.Should().BeGreaterThan(0);
        created.Path.Should().Be(Path.GetFullPath(filePath));

        var again = await _sut.EnsureIndexedAsync(filePath);
        again.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task ClearAllMediaMetadataAsync_resets_rankings_categories_and_open_counts()
    {
        await using var context = _contextFactory.CreateDbContext();
        var category = new VideoCategory { Name = "Drama", SortOrder = 0 };
        var file = new MediaFile
        {
            Path = Path.Combine(_rootDirectory, "reset.mp4"),
            Name = "reset.mp4",
            Extension = ".mp4",
            VecesAbierto = 4,
            RankingCalidad = 5,
            RankingContenido = 5,
            RankingGusto = 5
        };
        file.Categories.Add(category);
        context.MediaFiles.Add(file);
        await context.SaveChangesAsync();

        var result = await _sut.ClearAllMediaMetadataAsync();

        result.HasChanges.Should().BeTrue();
        result.CategoriesDeleted.Should().Be(1);

        await using var verifyContext = _contextFactory.CreateDbContext();
        var stored = await verifyContext.MediaFiles.FindAsync(file.Id);
        stored!.VecesAbierto.Should().Be(0);
        stored.RankingCalidad.Should().Be(0);
        verifyContext.VideoCategories.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateCategoriesAsync_assigns_existing_categories_to_indexed_file()
    {
        var filePath = Path.Combine(_rootDirectory, "tagged.mp4");
        await File.WriteAllTextAsync(filePath, "video");
        var indexed = await SeedIndexedFileAsync("tagged.mp4", filePath);

        await using var context = _contextFactory.CreateDbContext();
        var category = new VideoCategory { Name = "Sci-Fi", SortOrder = 0 };
        context.VideoCategories.Add(category);
        await context.SaveChangesAsync();

        var updated = await _sut.UpdateCategoriesAsync(indexed.Id, [category.Id]);

        updated.Categories.Should().ContainSingle(item => item.Name == "Sci-Fi");
    }

    private async Task<MediaFile> SeedIndexedFileAsync(
        string fileName,
        string? absolutePath = null,
        int vecesAbierto = 0)
    {
        absolutePath ??= Path.Combine(_rootDirectory, fileName);

        await using var context = _contextFactory.CreateDbContext();
        var entity = new MediaFile
        {
            Path = absolutePath,
            Name = fileName,
            Extension = Path.GetExtension(fileName),
            VecesAbierto = vecesAbierto
        };
        context.MediaFiles.Add(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    public void Dispose()
    {
        _contextFactory.Dispose();

        if (Directory.Exists(_rootDirectory))
            Directory.Delete(_rootDirectory, recursive: true);
    }
}
