using FluentAssertions;
using MediaVault.LinkHub.Domain.Enums;
using MediaVault.LinkHub.Infrastructure.Media;
using MediaVault.LinkHub.Infrastructure.Services;
using MediaVault.LinkHub.Tests.Infrastructure;

namespace MediaVault.LinkHub.Tests.Services;

public sealed class SuggestionServiceTests : IDisposable
{
    private readonly TestDbContextFactory _contextFactory = new();
    private readonly string _storageRoot;
    private readonly SuggestionService _sut;

    // PNG 1x1 mínimo válido.
    private static readonly byte[] TinyPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
        0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, 0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
        0x00, 0x00, 0x03, 0x00, 0x01, 0x00, 0x05, 0xFE, 0x02, 0xFE, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45,
        0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    ];

    public SuggestionServiceTests()
    {
        _storageRoot = Path.Combine(Path.GetTempPath(), "MediaVaultSuggestionTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_storageRoot);
        _sut = new SuggestionService(_contextFactory, new SuggestionImageStorage(_storageRoot));
    }

    [Fact]
    public async Task CreateAsync_persists_text_kind_and_images()
    {
        var imagePath = CreateTempPng("shot.png");

        var created = await _sut.CreateAsync("  Botón desalineado  ", SuggestionKind.Error, [imagePath]);

        created.Id.Should().BeGreaterThan(0);
        created.Texto.Should().Be("Botón desalineado");
        created.Tipo.Should().Be(SuggestionKind.Error);
        created.Resuelto.Should().BeFalse();
        created.Attachments.Should().ContainSingle();
        File.Exists(created.Attachments.First().FilePath).Should().BeTrue();
    }

    [Fact]
    public async Task SetResolvedAsync_sets_flag_and_timestamp()
    {
        var created = await _sut.CreateAsync("Mejora de filtros", SuggestionKind.Mejora);

        var resolved = await _sut.SetResolvedAsync(created.Id, true);

        resolved.Resuelto.Should().BeTrue();
        resolved.FechaResuelto.Should().NotBeNull();
        resolved.FechaResuelto.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var pending = await _sut.SetResolvedAsync(created.Id, false);
        pending.Resuelto.Should().BeFalse();
        pending.FechaResuelto.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_orders_pending_first()
    {
        var a = await _sut.CreateAsync("Pendiente A", SuggestionKind.Mejora);
        var b = await _sut.CreateAsync("Pendiente B", SuggestionKind.Error);
        await _sut.SetResolvedAsync(a.Id, true);

        var all = await _sut.GetAllAsync();

        all.Should().HaveCount(2);
        all[0].Id.Should().Be(b.Id);
        all[1].Id.Should().Be(a.Id);
    }

    [Fact]
    public async Task DeleteAsync_removes_managed_image_files()
    {
        var imagePath = CreateTempPng("delete-me.png");
        var created = await _sut.CreateAsync("Con captura", SuggestionKind.Otro, [imagePath]);
        var managedPath = created.Attachments.Single().FilePath;

        await _sut.DeleteAsync(created.Id);

        (await _sut.GetByIdAsync(created.Id)).Should().BeNull();
        File.Exists(managedPath).Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_rejects_empty_text()
    {
        var act = () => _sut.CreateAsync("   ", SuggestionKind.Mejora);
        await act.Should().ThrowAsync<ArgumentException>().WithParameterName("texto");
    }

    private string CreateTempPng(string fileName)
    {
        var path = Path.Combine(_storageRoot, fileName);
        File.WriteAllBytes(path, TinyPng);
        return path;
    }

    public void Dispose()
    {
        _contextFactory.Dispose();
        try
        {
            if (Directory.Exists(_storageRoot))
                Directory.Delete(_storageRoot, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
