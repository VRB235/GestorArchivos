using FluentAssertions;
using MediaVault.LinkHub.Domain.Enums;
using MediaVault.LinkHub.Infrastructure.Services;
using MediaVault.LinkHub.Tests.Infrastructure;

namespace MediaVault.LinkHub.Tests.Services;

public sealed class WebLinkServiceTests : IDisposable
{
    private readonly TestDbContextFactory _contextFactory = new();
    private readonly WebLinkService _sut;

    public WebLinkServiceTests() =>
        _sut = new WebLinkService(_contextFactory);

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

    public void Dispose() => _contextFactory.Dispose();
}
