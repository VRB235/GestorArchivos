namespace MediaVault.LinkHub.App.Shell;

public interface IAppDialogService
{
    bool ConfirmYesNo(string title, string message, AppDialogKind kind = AppDialogKind.Warning);

    void ShowMessage(string title, string message, AppDialogKind kind = AppDialogKind.Information);
}
