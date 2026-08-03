using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using MediaVault.LinkHub.Application.Media;

namespace MediaVault.LinkHub.App.Controls;

public partial class StarRatingControl : UserControl
{
    public static readonly DependencyProperty RatingProperty =
        DependencyProperty.Register(
            nameof(Rating),
            typeof(int),
            typeof(StarRatingControl),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnRatingChanged));

    public static readonly DependencyProperty IsInteractiveProperty =
        DependencyProperty.Register(
            nameof(IsInteractive),
            typeof(bool),
            typeof(StarRatingControl),
            new PropertyMetadata(true, OnIsInteractiveChanged));

    private static readonly Brush FilledStarBrush = new SolidColorBrush(Color.FromRgb(251, 191, 36));
    private static readonly Brush EmptyStarBrush = new SolidColorBrush(Color.FromRgb(156, 163, 175));

    private readonly TextBlock[] _starBlocks = new TextBlock[MediaFileRankingScale.MaxStars];

    static StarRatingControl()
    {
        FilledStarBrush.Freeze();
        EmptyStarBrush.Freeze();
    }

    public StarRatingControl()
    {
        InitializeComponent();
        BuildStars();
        UpdateStarVisuals();
    }

    public int Rating
    {
        get => (int)GetValue(RatingProperty);
        set => SetValue(RatingProperty, Math.Clamp(value, 0, MediaFileRankingScale.MaxStars));
    }

    public bool IsInteractive
    {
        get => (bool)GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    private void BuildStars()
    {
        StarsPanel.Children.Clear();

        for (var index = 0; index < MediaFileRankingScale.MaxStars; index++)
        {
            var starNumber = index + 1;
            var starBlock = new TextBlock
            {
                Text = "\uE734",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 20,
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = IsInteractive ? Cursors.Hand : Cursors.Arrow,
                ToolTip = $"{starNumber} estrella{(starNumber == 1 ? string.Empty : "s")}"
            };

            starBlock.MouseLeftButtonUp += (_, _) => OnStarClicked(starNumber);
            _starBlocks[index] = starBlock;
            StarsPanel.Children.Add(starBlock);
        }
    }

    private void OnStarClicked(int starNumber)
    {
        if (!IsInteractive)
            return;

        Rating = Rating == starNumber ? 0 : starNumber;
    }

    private static void OnIsInteractiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not StarRatingControl control)
            return;

        foreach (var starBlock in control._starBlocks)
        {
            if (starBlock is null)
                continue;

            starBlock.Cursor = control.IsInteractive ? Cursors.Hand : Cursors.Arrow;
        }
    }

    private static void OnRatingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StarRatingControl control)
            control.UpdateStarVisuals();
    }

    private void UpdateStarVisuals()
    {
        for (var index = 0; index < _starBlocks.Length; index++)
        {
            var starBlock = _starBlocks[index];
            var filled = index < Rating;
            starBlock.Text = filled ? "\uE735" : "\uE734";
            starBlock.Foreground = filled ? FilledStarBrush : EmptyStarBrush;
        }
    }
}
