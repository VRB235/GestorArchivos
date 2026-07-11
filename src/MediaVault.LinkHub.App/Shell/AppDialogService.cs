using System.Windows;
using MediaVault.LinkHub.App.Views;

namespace MediaVault.LinkHub.App.Shell;

public sealed class AppDialogService : IAppDialogService
{
    public bool ConfirmYesNo(string title, string message, AppDialogKind kind = AppDialogKind.Warning)
    {
        var dialog = CreateDialog(title, message, kind, AppDialogButtons.YesNo);
        dialog.ShowDialog();
        return dialog.Result == AppDialogResult.Yes;
    }

    public void ShowMessage(string title, string message, AppDialogKind kind = AppDialogKind.Information)
    {
        var dialog = CreateDialog(title, message, kind, AppDialogButtons.Ok);
        dialog.ShowDialog();
    }

    private static AppDialogWindow CreateDialog(
        string title,
        string message,
        AppDialogKind kind,
        AppDialogButtons buttons)
    {
        var owner = System.Windows.Application.Current.MainWindow;
        var dialog = new AppDialogWindow(title, message, kind, buttons);

        if (owner is not null && owner.IsLoaded)
            dialog.Owner = owner;

        dialog.WindowStartupLocation = dialog.Owner is null
            ? WindowStartupLocation.CenterScreen
            : WindowStartupLocation.CenterOwner;

        return dialog;
    }
}
