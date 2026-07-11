using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MediaVault.LinkHub.App.Security;
using MediaVault.LinkHub.App.ViewModels;

namespace MediaVault.LinkHub.App.Views;

public partial class SecurityGateWindow : Window
{
    private readonly SecurityGateViewModel _viewModel = new();

    public SecurityAccessMode AccessMode { get; private set; } = SecurityAccessMode.Full;

    public SecurityGateWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.AccessGranted += OnAccessGranted;
        Loaded += (_, _) => PinBox.Focus();
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(SecurityGateViewModel.IsPasswordStep))
                return;

            PinBox.Clear();
            PasswordBox.Clear();

            if (_viewModel.IsPasswordStep)
                PasswordBox.Focus();
            else
                PinBox.Focus();
        };
    }

    private void OnAccessGranted(SecurityAccessMode accessMode)
    {
        AccessMode = accessMode;
        DialogResult = true;
    }

    private void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        SyncInputsBeforeSubmit();
        _viewModel.SubmitCommand.Execute(null);

        if (string.IsNullOrEmpty(_viewModel.ErrorMessage))
            return;

        if (_viewModel.IsPasswordStep)
            PasswordBox.Clear();
        else
            PinBox.Clear();
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        SyncInputsBeforeSubmit();
        if (_viewModel.SubmitCommand.CanExecute(null))
            _viewModel.SubmitCommand.Execute(null);
    }

    private void SyncInputsBeforeSubmit()
    {
        if (_viewModel.IsPasswordStep)
            _viewModel.PasswordInput = PasswordBox.Password;
        else
            _viewModel.PinInput = PinBox.Password;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (DialogResult != true)
            DialogResult = false;

        base.OnClosing(e);
    }
}
