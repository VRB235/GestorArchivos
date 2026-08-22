using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using MediaVault.LinkHub.App.ViewModels;

namespace MediaVault.LinkHub.App.Views;

public partial class ActressScrapeSessionWindow : Window
{
    public ActressScrapeSessionWindow(ActressScrapeSessionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.InitializeAsync().ConfigureAwait(true);
        Closed += (_, _) => StopHoverMedia();
    }

    private ActressScrapeSessionViewModel? ViewModel =>
        DataContext as ActressScrapeSessionViewModel;

    private void Thumbnail_OnMouseEnter(object sender, MouseEventArgs e)
    {
        if (ViewModel is null || sender is not FrameworkElement { DataContext: ScrapedVideoListItem item })
            return;

        ViewModel.BeginHoverPreview(item);

        if (ViewModel.HoverPreviewIsVideo
            && !string.IsNullOrWhiteSpace(ViewModel.HoverPreviewMediaUrl)
            && Uri.TryCreate(ViewModel.HoverPreviewMediaUrl, UriKind.Absolute, out var mediaUri))
        {
            try
            {
                HoverPreviewMedia.Source = mediaUri;
                HoverPreviewMedia.Position = TimeSpan.Zero;
                HoverPreviewMedia.Play();
            }
            catch
            {
                // Si el CDN bloquea el stream, queda el fallback de imagen.
                ViewModel.HoverPreviewIsVideo = false;
            }
        }
    }

    private void Thumbnail_OnMouseLeave(object sender, MouseEventArgs e)
    {
        StopHoverMedia();
        ViewModel?.EndHoverPreview();
    }

    private void HoverPreviewMedia_OnMediaEnded(object sender, RoutedEventArgs e)
    {
        if (sender is not MediaElement media)
            return;

        media.Position = TimeSpan.Zero;
        media.Play();
    }

    private void StopHoverMedia()
    {
        try
        {
            HoverPreviewMedia.Stop();
            HoverPreviewMedia.Source = null;
        }
        catch
        {
            // ignore
        }
    }
}
