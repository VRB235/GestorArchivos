namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// Abre URLs HTTP/HTTPS en Firefox.
/// </summary>
public interface IBrowserLauncher
{
    /// <summary>
    /// Intenta abrir la URL en Firefox. Devuelve false si la URL no es válida o Firefox no está disponible.
    /// </summary>
    bool TryOpenInFirefox(string url);
}
