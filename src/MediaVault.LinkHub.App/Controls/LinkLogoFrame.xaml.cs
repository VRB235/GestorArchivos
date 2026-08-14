using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using MediaVault.LinkHub.Domain.Enums;
using MediaVault.LinkHub.Domain.Media;

namespace MediaVault.LinkHub.App.Controls;

public partial class LinkLogoFrame : UserControl
{
    public static readonly DependencyProperty LogoPathProperty =
        DependencyProperty.Register(
            nameof(LogoPath),
            typeof(string),
            typeof(LinkLogoFrame),
            new PropertyMetadata(null, OnVisualPropsChanged));

    public static readonly DependencyProperty CategoryProperty =
        DependencyProperty.Register(
            nameof(Category),
            typeof(LinkCategory),
            typeof(LinkLogoFrame),
            new PropertyMetadata(LinkCategory.Oficial, OnVisualPropsChanged));

    public static readonly DependencyProperty ZoomProperty =
        DependencyProperty.Register(
            nameof(Zoom),
            typeof(double),
            typeof(LinkLogoFrame),
            new PropertyMetadata(1.0, OnFitChanged));

    public static readonly DependencyProperty OffsetXProperty =
        DependencyProperty.Register(
            nameof(OffsetX),
            typeof(double),
            typeof(LinkLogoFrame),
            new PropertyMetadata(0.0, OnFitChanged));

    public static readonly DependencyProperty OffsetYProperty =
        DependencyProperty.Register(
            nameof(OffsetY),
            typeof(double),
            typeof(LinkLogoFrame),
            new PropertyMetadata(0.0, OnFitChanged));

    public static readonly DependencyProperty FrameSizeProperty =
        DependencyProperty.Register(
            nameof(FrameSize),
            typeof(double),
            typeof(LinkLogoFrame),
            new PropertyMetadata(96.0, OnFrameSizeChanged));

    public LinkLogoFrame()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshAll();
    }

    public string? LogoPath
    {
        get => (string?)GetValue(LogoPathProperty);
        set => SetValue(LogoPathProperty, value);
    }

    public LinkCategory Category
    {
        get => (LinkCategory)GetValue(CategoryProperty);
        set => SetValue(CategoryProperty, value);
    }

    public double Zoom
    {
        get => (double)GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    public double OffsetX
    {
        get => (double)GetValue(OffsetXProperty);
        set => SetValue(OffsetXProperty, value);
    }

    public double OffsetY
    {
        get => (double)GetValue(OffsetYProperty);
        set => SetValue(OffsetYProperty, value);
    }

    public double FrameSize
    {
        get => (double)GetValue(FrameSizeProperty);
        set => SetValue(FrameSizeProperty, value);
    }

    private static void OnVisualPropsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LinkLogoFrame frame)
            frame.RefreshAll();
    }

    private static void OnFitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LinkLogoFrame frame)
            frame.ApplyFitTransform();
    }

    private static void OnFrameSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LinkLogoFrame frame)
            frame.ApplyFrameSize();
    }

    private void RefreshAll()
    {
        ApplyFrameSize();
        ApplyBackgroundAndGlyph();
        LoadLogo();
        ApplyFitTransform();
    }

    private void ApplyFrameSize()
    {
        var size = Math.Max(24, FrameSize);
        RootGrid.Width = size;
        RootGrid.Height = size;
        Viewport.CornerRadius = new CornerRadius(12);
        ViewportClip.Rect = new Rect(0, 0, size, size);
        ViewportClip.RadiusX = 12;
        ViewportClip.RadiusY = 12;
        GlyphText.FontSize = size * 0.29;
        ApplyFitTransform();
    }

    private void ApplyBackgroundAndGlyph()
    {
        TileBackground.Background = CreateCategoryBrush(Category);
        GlyphText.Text = Category switch
        {
            LinkCategory.Oficial => "🌐",
            LinkCategory.Descarga => "⬇",
            LinkCategory.Gratis => "★",
            _ => "🔗"
        };
    }

    private void LoadLogo()
    {
        var path = LogoPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            LogoImage.Source = null;
            GlyphText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var decodeWidth = (int)Math.Clamp(FrameSize * WebLinkLogoFit.MaxZoom * 2, 128, 512);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(Path.GetFullPath(path), UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = decodeWidth;
            bitmap.EndInit();
            bitmap.Freeze();
            LogoImage.Source = bitmap;
            GlyphText.Visibility = Visibility.Collapsed;
        }
        catch
        {
            LogoImage.Source = null;
            GlyphText.Visibility = Visibility.Visible;
        }
    }

    private void ApplyFitTransform()
    {
        var zoom = WebLinkLogoFit.ClampZoom(Zoom);
        var offsetX = WebLinkLogoFit.ClampOffset(OffsetX);
        var offsetY = WebLinkLogoFit.ClampOffset(OffsetY);
        var size = Math.Max(24, FrameSize);
        // Con Stretch=Uniform, zoom 1 encaja la imagen completa. El exceso (zoom≠1)
        // se puede desplazar, pero el Viewport recorta todo lo que salga del cuadro.
        var pan = Math.Abs(zoom - 1.0) * size / 2.0;

        LogoScale.ScaleX = zoom;
        LogoScale.ScaleY = zoom;
        LogoTranslate.X = offsetX * pan;
        LogoTranslate.Y = offsetY * pan;
    }

    private static Brush CreateCategoryBrush(LinkCategory category)
    {
        var (startHex, endHex) = category switch
        {
            LinkCategory.Oficial => ("#2563EB", "#1D4ED8"),
            LinkCategory.Descarga => ("#7C3AED", "#5B21B6"),
            LinkCategory.Gratis => ("#059669", "#047857"),
            _ => ("#374151", "#1F2937")
        };

        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(startHex)!, 0));
        brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(endHex)!, 1));
        brush.Freeze();
        return brush;
    }
}
