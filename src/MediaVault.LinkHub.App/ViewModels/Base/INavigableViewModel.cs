namespace MediaVault.LinkHub.App.ViewModels.Base;

public interface INavigableViewModel
{
    string Title { get; }

    string Subtitle { get; }

    Task InitializeAsync();
}
