using System.Windows;
using System.Windows.Input;

using MediaVault.LinkHub.App.ViewModels;

namespace MediaVault.LinkHub.App.Views;

public partial class ProducerLinksWindow : Window
{
    public ProducerLinksWindow(ProducerLinksViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeAsync().ConfigureAwait(true);
        Closed += async (_, _) => await viewModel.OnClosingAsync().ConfigureAwait(true);
    }

    private void ScrapedVideo_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ProducerLinksViewModel vm)
            return;

        if (sender is FrameworkElement { DataContext: ScrapedVideoTileItem item })
            vm.OpenScrapedVideoCommand.Execute(item);
    }
}
