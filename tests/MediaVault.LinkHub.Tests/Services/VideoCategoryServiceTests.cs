using FluentAssertions;
using MediaVault.LinkHub.Infrastructure.Services;
using MediaVault.LinkHub.Tests.Infrastructure;

namespace MediaVault.LinkHub.Tests.Services;

public sealed class VideoCategoryServiceTests : IDisposable
{
    private readonly TestDbContextFactory _contextFactory = new();
    private readonly VideoCategoryService _sut;

    public VideoCategoryServiceTests() =>
        _sut = new VideoCategoryService(_contextFactory);

    [Fact]
    public async Task CreateAsync_assigns_incremental_sort_order()
    {
        var first = await _sut.CreateAsync("Acción");
        var second = await _sut.CreateAsync("Drama");

        first.SortOrder.Should().Be(0);
        second.SortOrder.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_trims_name_and_rejects_duplicates()
    {
        await _sut.CreateAsync("Comedia");

        var act = () => _sut.CreateAsync("  Comedia  ");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Comedia*");
    }

    [Fact]
    public async Task GetAllAsync_orders_by_sort_order_then_name()
    {
        await _sut.CreateAsync("Zeta");
        await _sut.CreateAsync("Alpha");

        var categories = await _sut.GetAllAsync();

        categories.Select(category => category.Name).Should().ContainInOrder("Zeta", "Alpha");
    }

    [Fact]
    public async Task UpdateAsync_rejects_duplicate_name_from_other_category()
    {
        var comedy = await _sut.CreateAsync("Comedia");
        await _sut.CreateAsync("Terror");

        var act = () => _sut.UpdateAsync(comedy.Id, "Terror");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Terror*");
    }

    [Fact]
    public async Task DeleteAsync_removes_existing_category()
    {
        var created = await _sut.CreateAsync("Documental");

        await _sut.DeleteAsync(created.Id);

        (await _sut.GetAllAsync()).Should().BeEmpty();
    }

    public void Dispose() => _contextFactory.Dispose();
}
