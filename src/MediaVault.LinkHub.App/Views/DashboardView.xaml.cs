using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

using CommunityToolkit.Mvvm.Input;

using MediaVault.LinkHub.App.Shell;
using MediaVault.LinkHub.App.ViewModels;
using MediaVault.LinkHub.Application.Media;

namespace MediaVault.LinkHub.App.Views;

public partial class DashboardView
{
    private static readonly TimeSpan CarouselSlideDuration = TimeSpan.FromMilliseconds(416);

    private const int CardsPerPage = VideoRecommendation.DefaultPickCount;
    private const double CardGap = 10;
    private const double ScrollEndEpsilon = 24;
    private const double MinCardWidth = 140;

    private DashboardViewModel? _subscribedViewModel;

    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => ScheduleCardWidthUpdate();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_subscribedViewModel is not null)
        {
            _subscribedViewModel.RecommendedVideos.CollectionChanged -= OnRecommendationCollectionChanged;
            _subscribedViewModel.RankedRecommendedVideos.CollectionChanged -= OnRecommendationCollectionChanged;
            _subscribedViewModel = null;
        }

        if (e.NewValue is DashboardViewModel viewModel)
        {
            _subscribedViewModel = viewModel;
            viewModel.RecommendedVideos.CollectionChanged += OnRecommendationCollectionChanged;
            viewModel.RankedRecommendedVideos.CollectionChanged += OnRecommendationCollectionChanged;
        }

        ScheduleCardWidthUpdate();
    }

    private void OnRecommendationCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        ScheduleCardWidthUpdate();

    private void RecommendationHost_OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateRecommendationCardWidth();

    private void RecommendationHost_OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
            ScheduleCardWidthUpdate();
    }

    private void ScheduleCardWidthUpdate()
    {
        // Tras Collapsed→Visible o al poblar items, el layout aún no tiene ActualWidth estable.
        Dispatcher.InvokeAsync(UpdateRecommendationCardWidth, DispatcherPriority.Loaded);
        Dispatcher.InvokeAsync(UpdateRecommendationCardWidth, DispatcherPriority.ContextIdle);
    }

    private void RecommendationScroller_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scroller || e.Delta == 0)
            return;

        // Rueda: un “tick” ≈ media página con la misma animación del carrusel.
        ScrollPage(scroller, forward: e.Delta < 0, pageFactor: 0.5);
        e.Handled = true;
    }

    private void MixedRecommendationPrev_OnClick(object sender, RoutedEventArgs e) =>
        ScrollPage(MixedRecommendationScroller, forward: false);

    private void RankedRecommendationPrev_OnClick(object sender, RoutedEventArgs e) =>
        ScrollPage(RankedRecommendationScroller, forward: false);

    private async void MixedRecommendationNext_OnClick(object sender, RoutedEventArgs e) =>
        await AdvanceCarouselAsync(
                MixedRecommendationScroller,
                loadMore: static vm => vm.LoadMoreRecommendationsCommand)
            .ConfigureAwait(true);

    private async void RankedRecommendationNext_OnClick(object sender, RoutedEventArgs e) =>
        await AdvanceCarouselAsync(
                RankedRecommendationScroller,
                loadMore: static vm => vm.LoadMoreRankedRecommendationsCommand)
            .ConfigureAwait(true);

    private void UpdateRecommendationCardWidth()
    {
        if (DataContext is not DashboardViewModel viewModel)
            return;

        // No usar ScrollViewer.ViewportWidth: con pocas cards colapsa al Extent del contenido.
        var hostWidth = ResolveHostWidth();
        if (hostWidth <= 1)
            return;

        var width = Math.Floor((hostWidth - CardGap * CardsPerPage) / CardsPerPage);
        var next = Math.Max(MinCardWidth, width);
        if (Math.Abs(viewModel.RecommendationCardWidth - next) < 0.5)
            return;

        viewModel.RecommendationCardWidth = next;
    }

    private double ResolveHostWidth()
    {
        if (MixedRecommendationHost.IsVisible && MixedRecommendationHost.ActualWidth > 1)
            return MixedRecommendationHost.ActualWidth;

        if (RankedRecommendationHost.IsVisible && RankedRecommendationHost.ActualWidth > 1)
            return RankedRecommendationHost.ActualWidth;

        // Host aún Collapsed: estimar desde el ancho del dashboard menos padding de cards.
        if (ActualWidth > 1)
            return Math.Max(1, ActualWidth - 64);

        return 0;
    }

    private async Task AdvanceCarouselAsync(
        ScrollViewer scroller,
        Func<DashboardViewModel, IAsyncRelayCommand> loadMore)
    {
        if (CanScrollForward(scroller))
        {
            ScrollPage(scroller, forward: true);
            return;
        }

        if (DataContext is not DashboardViewModel viewModel)
            return;

        var command = loadMore(viewModel);
        if (!command.CanExecute(null))
            return;

        var countBefore = scroller == MixedRecommendationScroller
            ? viewModel.RecommendedVideos.Count
            : viewModel.RankedRecommendedVideos.Count;

        await command.ExecuteAsync(null).ConfigureAwait(true);

        var countAfter = scroller == MixedRecommendationScroller
            ? viewModel.RecommendedVideos.Count
            : viewModel.RankedRecommendedVideos.Count;

        if (countAfter <= countBefore)
            return;

        await Dispatcher.InvokeAsync(
            () =>
            {
                UpdateRecommendationCardWidth();
                // Tras cargar +5, deslizar una página (no saltar al final).
                ScrollPage(scroller, forward: true);
            },
            DispatcherPriority.Loaded);
    }

    private static bool CanScrollForward(ScrollViewer scroller) =>
        scroller.ExtentWidth - scroller.ViewportWidth - scroller.HorizontalOffset > ScrollEndEpsilon;

    private void ScrollPage(ScrollViewer scroller, bool forward, double pageFactor = 1.0)
    {
        var pageWidth = ResolvePageWidth(scroller) * Math.Clamp(pageFactor, 0.25, 1.0);
        if (pageWidth <= 1)
            return;

        var maxOffset = Math.Max(0, scroller.ExtentWidth - scroller.ViewportWidth);
        var rawTarget = forward
            ? scroller.HorizontalOffset + pageWidth
            : scroller.HorizontalOffset - pageWidth;

        // Snap a múltiplos de página para que el carrusel alinee de 5 en 5.
        var fullPage = ResolvePageWidth(scroller);
        var target = fullPage > 1
            ? Math.Round(rawTarget / fullPage) * fullPage
            : rawTarget;

        ScrollViewerHorizontalAnimator.AnimateTo(
            scroller,
            Math.Clamp(target, 0, maxOffset),
            CarouselSlideDuration);
    }

    private double ResolvePageWidth(ScrollViewer scroller)
    {
        if (DataContext is DashboardViewModel viewModel && viewModel.RecommendationCardWidth > 1)
            return (viewModel.RecommendationCardWidth + CardGap) * CardsPerPage;

        return Math.Max(120, scroller.ViewportWidth);
    }
}
