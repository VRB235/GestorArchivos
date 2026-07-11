using FluentAssertions;
using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Infrastructure.Services;
using MediaVault.LinkHub.Tests.Infrastructure;

namespace MediaVault.LinkHub.Tests.Services;

public sealed class QuickNoteServiceTests : IDisposable
{
    private readonly TestDbContextFactory _contextFactory = new();
    private readonly QuickNoteService _sut;

    public QuickNoteServiceTests() =>
        _sut = new QuickNoteService(_contextFactory);

    [Fact]
    public async Task CreateAsync_trims_content_and_persists_note()
    {
        var created = await _sut.CreateAsync("  Nota de prueba  ");

        created.Id.Should().BeGreaterThan(0);
        created.Contenido.Should().Be("Nota de prueba");
        created.FechaCreacion.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateAsync_throws_when_content_is_empty()
    {
        var act = () => _sut.CreateAsync("   ");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("contenido");
    }

    [Fact]
    public async Task GetAllAsync_orders_by_creation_date_descending()
    {
        await _sut.CreateAsync("Antigua");
        await Task.Delay(15);
        await _sut.CreateAsync("Reciente");

        var notes = await _sut.GetAllAsync();

        notes.Should().HaveCount(2);
        notes[0].Contenido.Should().Be("Reciente");
        notes[1].Contenido.Should().Be("Antigua");
    }

    [Fact]
    public async Task UpdateAsync_replaces_content()
    {
        var created = await _sut.CreateAsync("Original");

        var updated = await _sut.UpdateAsync(created.Id, "  Editada  ");

        updated.Contenido.Should().Be("Editada");
        (await _sut.GetByIdAsync(created.Id))!.Contenido.Should().Be("Editada");
    }

    [Fact]
    public async Task DeleteAsync_is_idempotent_when_note_is_missing()
    {
        var act = () => _sut.DeleteAsync(999);

        await act.Should().NotThrowAsync();
    }

    public void Dispose() => _contextFactory.Dispose();
}
