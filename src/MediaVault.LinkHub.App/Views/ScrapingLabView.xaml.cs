using System.Windows;
using System.Windows.Controls;

using MediaVault.LinkHub.App.ViewModels;

namespace MediaVault.LinkHub.App.Views;

public partial class ScrapingLabView
{
    public ScrapingLabView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Unloaded += (_, _) => StopPreviewMedia();
    }

    private ScrapingLabViewModel? ViewModel => DataContext as ScrapingLabViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ScrapingLabViewModel oldVm)
            oldVm.PropertyChanged -= ViewModel_OnPropertyChanged;

        if (e.NewValue is ScrapingLabViewModel newVm)
            newVm.PropertyChanged += ViewModel_OnPropertyChanged;
    }

    private void ViewModel_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ScrapingLabViewModel.PreviewPlayToken)
            or nameof(ScrapingLabViewModel.PreviewMediaUrl))
        {
            TryPlayPreview();
        }

        if (e.PropertyName is nameof(ScrapingLabViewModel.PreviewIsVideo)
            && ViewModel is { PreviewIsVideo: false })
        {
            StopPreviewMedia();
        }
    }

    private void TryPlayPreview()
    {
        var vm = ViewModel;
        if (vm is null || !vm.PreviewIsVideo || string.IsNullOrWhiteSpace(vm.PreviewMediaUrl))
        {
            StopPreviewMedia();
            return;
        }

        if (!Uri.TryCreate(vm.PreviewMediaUrl, UriKind.Absolute, out var mediaUri))
        {
            vm.NotifyPreviewFailed(vm.CurrentPreviewGeneration, "URL de preview inválida.");
            return;
        }

        try
        {
            PreviewMedia.Source = mediaUri;
            PreviewMedia.Position = TimeSpan.Zero;
            PreviewMedia.Play();
        }
        catch (Exception ex)
        {
            vm.NotifyPreviewFailed(vm.CurrentPreviewGeneration, ex.Message);
        }
    }

    private void PreviewMedia_OnMediaOpened(object sender, RoutedEventArgs e)
    {
        ViewModel?.NotifyPreviewOpened(ViewModel.CurrentPreviewGeneration);
    }

    private void PreviewMedia_OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        ViewModel?.NotifyPreviewFailed(
            ViewModel.CurrentPreviewGeneration,
            e.ErrorException?.Message ?? "MediaFailed");
    }

    private void PreviewMedia_OnMediaEnded(object sender, RoutedEventArgs e)
    {
        if (sender is not MediaElement media)
            return;

        media.Position = TimeSpan.Zero;
        media.Play();
    }

    private void StopPreviewMedia()
    {
        try
        {
            PreviewMedia.Stop();
            PreviewMedia.Source = null;
        }
        catch
        {
            // ignore
        }
    }
}
