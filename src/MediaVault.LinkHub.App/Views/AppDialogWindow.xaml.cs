using System.Windows;
using System.Windows.Media;
using MediaVault.LinkHub.App.Shell;

namespace MediaVault.LinkHub.App.Views;

public partial class AppDialogWindow : Window
{
    private readonly AppDialogButtons _buttons;

    public AppDialogResult Result { get; private set; } = AppDialogResult.None;

    public AppDialogWindow(string title, string message, AppDialogKind kind, AppDialogButtons buttons)
    {
        _buttons = buttons;
        InitializeComponent();

        TitleText.Text = title;
        MessageText.Text = message;

        ApplyKind(kind);
        ApplyButtons(buttons, kind);
    }

    private void ApplyKind(AppDialogKind kind)
    {
        switch (kind)
        {
            case AppDialogKind.Question:
                IconGlyph.Text = "\uE897";
                IconGlyph.Foreground = (Brush)FindResource("AccentBrush");
                break;
            case AppDialogKind.Warning:
                IconGlyph.Text = "\uE7BA";
                IconGlyph.Foreground = (Brush)FindResource("DangerBrush");
                break;
            default:
                IconGlyph.Text = "\uE946";
                IconGlyph.Foreground = (Brush)FindResource("AccentBrush");
                break;
        }
    }

    private void ApplyButtons(AppDialogButtons buttons, AppDialogKind kind)
    {
        var isYesNo = buttons == AppDialogButtons.YesNo;
        OkButton.Visibility = isYesNo ? Visibility.Collapsed : Visibility.Visible;
        YesButton.Visibility = isYesNo ? Visibility.Visible : Visibility.Collapsed;
        NoButton.Visibility = isYesNo ? Visibility.Visible : Visibility.Collapsed;

        if (!isYesNo)
            return;

        YesButton.Style = kind == AppDialogKind.Warning
            ? (Style)FindResource("DangerButtonStyle")
            : (Style)FindResource("PrimaryButtonStyle");
    }

    private void YesButton_Click(object sender, RoutedEventArgs e)
    {
        Result = AppDialogResult.Yes;
        DialogResult = true;
        Close();
    }

    private void NoButton_Click(object sender, RoutedEventArgs e)
    {
        Result = AppDialogResult.No;
        DialogResult = false;
        Close();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Result = AppDialogResult.Ok;
        DialogResult = true;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Result = _buttons == AppDialogButtons.YesNo
            ? AppDialogResult.No
            : AppDialogResult.None;
        DialogResult = false;
        Close();
    }
}
