using FluentAssertions;
using MediaVault.LinkHub.Domain.Enums;
using MediaVault.LinkHub.Infrastructure.Media;
using MediaVault.LinkHub.Infrastructure.Services;
using MediaVault.LinkHub.Tests.Infrastructure;

namespace MediaVault.LinkHub.Tests.Services;

public sealed class WebLinkServiceTests : IDisposable
{
    private readonly TestDbContextFactory _contextFactory = new();
    private readonly string _logoRoot;
    private readonly LinkLogoStorage _logoStorage;
    private readonly WebLinkService _sut;

    public WebLinkServiceTests()
    {
        _logoRoot = Path.Combine(Path.GetTempPath(), "MediaVaultLinkHubTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_logoRoot);
        _logoStorage = new LinkLogoStorage(_logoRoot);
        _sut = new WebLinkService(_contextFactory, _logoStorage);
    }

    [Fact]
    public async Task CreateAsync_normalizes_url_without_scheme()
    {
        var created = await _sut.CreateAsync(
            "GitLab",
            "gitlab.com",
            LinkCategory.Oficial);

        created.Url.Should().Be("https://gitlab.com/");
        created.Nombre.Should().Be("GitLab");
        created.Categoria.Should().Be(LinkCategory.Oficial);
    }

    [Fact]
    public async Task CreateAsync_rejects_invalid_url()
    {
        var act = () => _sut.CreateAsync("Sitio", "not a url", LinkCategory.Gratis);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("url");
    }

    [Fact]
    public async Task GetByCategoryAsync_returns_only_matching_links()
    {
        await _sut.CreateAsync("Oficial", "https://example.com/a", LinkCategory.Oficial);
        await _sut.CreateAsync("Gratis", "https://example.com/b", LinkCategory.Gratis);

        var officialLinks = await _sut.GetByCategoryAsync(LinkCategory.Oficial);

        officialLinks.Should().ContainSingle(link => link.Nombre == "Oficial");
    }

    [Fact]
    public async Task MarkAsUserUpdatedAsync_stores_visit_date_in_utc()
    {
        var created = await _sut.CreateAsync("Portal", "https://portal.test", LinkCategory.Descarga);
        var localVisit = new DateTime(2026, 3, 15, 10, 30, 0, DateTimeKind.Local);

        var updated = await _sut.MarkAsUserUpdatedAsync(created.Id, localVisit);

        updated.FechaUltimaActualizacion.Should().Be(localVisit.ToUniversalTime());
    }

    [Fact]
    public async Task DeleteAsync_is_idempotent_when_link_is_missing()
    {
        var act = () => _sut.DeleteAsync(404);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CreateAsync_copies_logo_into_managed_storage_and_survives_source_deletion()
    {
        var source = CreateTempImage("source-logo.png");

        var created = await _sut.CreateAsync(
            "Con logo",
            "https://logo.test",
            LinkCategory.Oficial,
            source);

        created.LogoPath.Should().NotBeNullOrWhiteSpace();
        created.LogoPath.Should().NotBe(Path.GetFullPath(source));
        _logoStorage.IsManagedPath(created.LogoPath).Should().BeTrue();
        File.Exists(created.LogoPath!).Should().BeTrue();

        File.Delete(source);
        File.Exists(created.LogoPath!).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_replaces_logo_and_deletes_previous_managed_file()
    {
        var firstSource = CreateTempImage("first.png");
        var secondSource = CreateTempImage("second.png");

        var created = await _sut.CreateAsync(
            "Cambio logo",
            "https://logo-change.test",
            LinkCategory.Gratis,
            firstSource);

        var previousManaged = created.LogoPath!;
        File.Exists(previousManaged).Should().BeTrue();

        var updated = await _sut.UpdateAsync(
            created.Id,
            created.Nombre,
            created.Url,
            created.Categoria,
            secondSource);

        updated.LogoPath.Should().NotBe(previousManaged);
        File.Exists(updated.LogoPath!).Should().BeTrue();
        File.Exists(previousManaged).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_removes_managed_logo_file()
    {
        var source = CreateTempImage("to-delete.png");
        var created = await _sut.CreateAsync(
            "Borrar",
            "https://delete-logo.test",
            LinkCategory.Descarga,
            source);

        var managed = created.LogoPath!;
        File.Exists(managed).Should().BeTrue();

        await _sut.DeleteAsync(created.Id);

        File.Exists(managed).Should().BeFalse();
    }

    [Fact]
    public async Task MigrateExternalLogosAsync_copies_existing_external_paths()
    {
        var external = CreateTempImage("external.png");
        var created = await _sut.CreateAsync("Sin migrar aún", "https://migrate.test", LinkCategory.Oficial);

        await using (var context = await _contextFactory.CreateDbContextAsync())
        {
            var entity = await context.WebLinks.FindAsync(created.Id);
            entity!.LogoPath = external;
            await context.SaveChangesAsync();
        }

        await _sut.MigrateExternalLogosAsync();

        var migrated = await _sut.GetByIdAsync(created.Id);
        migrated!.LogoPath.Should().NotBe(Path.GetFullPath(external));
        _logoStorage.IsManagedPath(migrated.LogoPath).Should().BeTrue();
        File.Exists(migrated.LogoPath!).Should().BeTrue();
    }

    public void Dispose()
    {
        _contextFactory.Dispose();
        try
        {
            if (Directory.Exists(_logoRoot))
                Directory.Delete(_logoRoot, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static string CreateTempImage(string fileName)
    {
        var path = Path.Combine(Path.GetTempPath(), "MediaVaultLinkHubTests", Guid.NewGuid().ToString("N"), fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // PNG mínimo 1x1 válido
        File.WriteAllBytes(path,
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41,
            0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
            0x00, 0x00, 0x03, 0x00, 0x01, 0x00, 0x05, 0xFE,
            0x02, 0xFE, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45,
            0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
        ]);
        return path;
    }
}
