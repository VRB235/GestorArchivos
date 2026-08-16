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
    public async Task OpenFileAsync_increments_veces_abierto()
    {
        var filePath = Path.Combine(_rootDirectory, "opened.jpg");
        await File.WriteAllTextAsync(filePath, "image");
        var indexed = await SeedIndexedFileAsync("opened.jpg", filePath);

        var opened = await _sut.OpenFileAsync(indexed.Id);

        opened.Should().NotBeNull();
        opened!.VecesAbierto.Should().Be(1);

        var again = await _sut.OpenFileAsync(indexed.Id);
        again.Should().NotBeNull();
        again!.VecesAbierto.Should().Be(2);
    }

    [Fact]
    public async Task EnsureIndexedAsync_then_OpenFileAsync_counts_first_open()
    {
        var filePath = Path.Combine(_rootDirectory, "first-open.mp4");
        await File.WriteAllTextAsync(filePath, "video");

        var indexed = await _sut.EnsureIndexedAsync(filePath);
        indexed.VecesAbierto.Should().Be(0);

        var opened = await _sut.OpenFileAsync(indexed.Id);

        opened.Should().NotBeNull();
        opened!.VecesAbierto.Should().Be(1);
    }

    [Fact]
    public async Task MoveFileAsync_moves_indexed_file_and_updates_path()
    {
        var sourceDir = Path.Combine(_rootDirectory, "origen");
        var destDir = Path.Combine(_rootDirectory, "destino");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destDir);

        var sourcePath = Path.Combine(sourceDir, "clip.mp4");
        await File.WriteAllTextAsync(sourcePath, "video");
        var indexed = await SeedIndexedFileAsync("clip.mp4", sourcePath, vecesAbierto: 2);

        var moved = await _sut.MoveFileAsync(sourcePath, destDir, _rootDirectory);

        moved.Should().NotBeNull();
        moved!.Id.Should().Be(indexed.Id);
        moved.VecesAbierto.Should().Be(2);
        File.Exists(sourcePath).Should().BeFalse();
        File.Exists(moved.Path).Should().BeTrue();
        Path.GetDirectoryName(moved.Path).Should().Be(Path.GetFullPath(destDir));
    }

    [Fact]
    public async Task MoveFileAsync_rejects_destination_outside_index_root()
    {
        var sourcePath = Path.Combine(_rootDirectory, "stay.mp4");
        await File.WriteAllTextAsync(sourcePath, "video");

        var outside = Path.Combine(Path.GetTempPath(), "MediaVaultOutsideMove", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);

        var act = () => _sut.MoveFileAsync(sourcePath, outside, _rootDirectory);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*fuera del directorio*");
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

    [Fact]
    public async Task CreateDirectoryAsync_creates_folder_under_parent()
    {
        await _sut.CreateDirectoryAsync(_rootDirectory, "NuevaCarpeta");

        Directory.Exists(Path.Combine(_rootDirectory, "NuevaCarpeta")).Should().BeTrue();
    }

    [Fact]
    public async Task CreateDirectoryAsync_rejects_invalid_name()
    {
        var act = () => _sut.CreateDirectoryAsync(_rootDirectory, "invalido/nombre");

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteDirectoryAsync_removes_folder_and_indexed_files()
    {
        var folder = Path.Combine(_rootDirectory, "ParaBorrar");
        Directory.CreateDirectory(folder);
        var filePath = Path.Combine(folder, "clip.mp4");
        await File.WriteAllTextAsync(filePath, "video");
        await SeedIndexedFileAsync("clip.mp4", filePath);

        await _sut.DeleteDirectoryAsync(folder, _rootDirectory);

        Directory.Exists(folder).Should().BeFalse();
        (await _sut.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteDirectoryAsync_rejects_index_root()
    {
        var act = () => _sut.DeleteDirectoryAsync(_rootDirectory, _rootDirectory);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*raíz*");
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
