namespace MediaVault.LinkHub.App.Models;

public sealed class NavigationItem
{
    public required string Title { get; init; }

    public required string Icon { get; init; }

    public required string Target { get; init; }
}
