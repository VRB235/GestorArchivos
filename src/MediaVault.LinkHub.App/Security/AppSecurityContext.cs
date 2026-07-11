namespace MediaVault.LinkHub.App.Security;

public static class AppSecurityContext
{
    public static SecurityAccessMode AccessMode { get; internal set; } = SecurityAccessMode.Full;

    public static bool IsMaintenanceMode => AccessMode == SecurityAccessMode.Maintenance;
}
