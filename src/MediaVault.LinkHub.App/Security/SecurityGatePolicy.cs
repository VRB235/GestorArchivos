namespace MediaVault.LinkHub.App.Security;

internal static class SecurityGatePolicy
{
    private const string MaintenancePin = "2589";
    private const string AdminPin = "2332";
    private const string AdminPassword = "YoVioleAAisha";

    public static SecurityPinValidationResult ValidatePin(string pin)
    {
        if (pin == MaintenancePin)
            return SecurityPinValidationResult.Maintenance();

        if (pin == AdminPin)
            return SecurityPinValidationResult.NeedsPasswordStep();

        return SecurityPinValidationResult.Invalid();
    }

    public static bool ValidatePassword(string password) =>
        string.Equals(password, AdminPassword, StringComparison.Ordinal);
}

internal readonly struct SecurityPinValidationResult
{
    private SecurityPinValidationResult(bool isValid, bool requiresPassword, SecurityAccessMode? accessMode)
    {
        IsValid = isValid;
        RequiresPassword = requiresPassword;
        AccessMode = accessMode;
    }

    public bool IsValid { get; }

    public bool RequiresPassword { get; }

    public SecurityAccessMode? AccessMode { get; }

    public static SecurityPinValidationResult Invalid() => new(false, false, null);

    public static SecurityPinValidationResult Maintenance() =>
        new(true, false, SecurityAccessMode.Maintenance);

    public static SecurityPinValidationResult NeedsPasswordStep() =>
        new(true, true, null);
}
