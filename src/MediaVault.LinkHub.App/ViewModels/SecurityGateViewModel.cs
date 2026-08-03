using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediaVault.LinkHub.App.Security;

namespace MediaVault.LinkHub.App.ViewModels;

public partial class SecurityGateViewModel : ObservableObject
{
    [ObservableProperty]
    private string _pinInput = string.Empty;

    [ObservableProperty]
    private string _passwordInput = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isPasswordStep;

    public string Title => IsPasswordStep ? "Verificación adicional" : "Acceso seguro";

    public string Subtitle => IsPasswordStep
        ? "Ingrese la contraseña de administrador para continuar."
        : "Ingrese el PIN de seguridad para acceder a MediaVault & LinkHub.";

    public string PrimaryActionLabel => IsPasswordStep ? "Validar contraseña" : "Continuar";

    public event Action<SecurityAccessMode>? AccessGranted;

    partial void OnIsPasswordStepChanged(bool value)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(PrimaryActionLabel));
    }

    [RelayCommand]
    private void Submit()
    {
        ErrorMessage = null;

        if (!IsPasswordStep)
        {
            ValidatePin();
            return;
        }

        ValidatePassword();
    }

    private void ValidatePin()
    {
        if (string.IsNullOrWhiteSpace(PinInput))
        {
            ErrorMessage = "El PIN es obligatorio.";
            return;
        }

        var result = SecurityGatePolicy.ValidatePin(PinInput.Trim());
        if (!result.IsValid)
        {
            ErrorMessage = "El PIN es incorrecto.";
            PinInput = string.Empty;
            return;
        }

        if (result.RequiresPassword)
        {
            IsPasswordStep = true;
            PinInput = string.Empty;
            PasswordInput = string.Empty;
            return;
        }

        AccessGranted?.Invoke(result.AccessMode!.Value);
    }

    private void ValidatePassword()
    {
        if (string.IsNullOrEmpty(PasswordInput))
        {
            ErrorMessage = "La contraseña es obligatoria.";
            return;
        }

        if (!SecurityGatePolicy.ValidatePassword(PasswordInput))
        {
            ErrorMessage = "Contraseña incorrecta. Intente nuevamente.";
            PasswordInput = string.Empty;
            return;
        }

        AccessGranted?.Invoke(SecurityAccessMode.Full);
    }
}
