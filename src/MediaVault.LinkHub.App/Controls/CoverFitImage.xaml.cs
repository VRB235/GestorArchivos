using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MediaVault.LinkHub.App.Controls;

public partial class CoverFitImage : UserControl
{
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(
            nameof(Source),
            typeof(ImageSource),
            typeof(CoverFitImage),
            new PropertyMetadata(null, OnSourceChanged));

    public CoverFitImage()
    {
        InitializeComponent();
    }

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not CoverFitImage control)
            return;

        var source = e.NewValue as ImageSource;
        control.BackdropImage.Source = source;
        control.ForegroundImage.Source = source;
    }
}
