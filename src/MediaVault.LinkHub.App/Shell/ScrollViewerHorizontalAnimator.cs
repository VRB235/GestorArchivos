using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace MediaVault.LinkHub.App.Shell;

/// <summary>
/// Anima <see cref="ScrollViewer.HorizontalOffset"/> (no es DP animable por sí solo).
/// </summary>
internal static class ScrollViewerHorizontalAnimator
{
    private static readonly DependencyProperty HorizontalOffsetProxyProperty =
        DependencyProperty.RegisterAttached(
            "HorizontalOffsetProxy",
            typeof(double),
            typeof(ScrollViewerHorizontalAnimator),
            new PropertyMetadata(0d, OnHorizontalOffsetProxyChanged));

    public static void AnimateTo(
        ScrollViewer scrollViewer,
        double targetOffset,
        TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(scrollViewer);

        var maxOffset = Math.Max(0, scrollViewer.ExtentWidth - scrollViewer.ViewportWidth);
        var to = Math.Clamp(targetOffset, 0, maxOffset);
        var from = scrollViewer.HorizontalOffset;

        if (Math.Abs(to - from) < 0.5)
            return;

        scrollViewer.BeginAnimation(HorizontalOffsetProxyProperty, null);
        scrollViewer.SetValue(HorizontalOffsetProxyProperty, from);

        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = duration,
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        animation.Completed += (_, _) =>
        {
            scrollViewer.BeginAnimation(HorizontalOffsetProxyProperty, null);
            scrollViewer.ScrollToHorizontalOffset(to);
        };

        scrollViewer.BeginAnimation(HorizontalOffsetProxyProperty, animation);
    }

    private static void OnHorizontalOffsetProxyChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer scrollViewer && e.NewValue is double offset)
            scrollViewer.ScrollToHorizontalOffset(offset);
    }
}
