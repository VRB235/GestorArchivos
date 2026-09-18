namespace MediaVault.LinkHub.App.ViewModels;

/// <summary>
/// Estado de verificación de carga del preview en Scraping Lab.
/// </summary>
public enum ScrapingLabPreviewState
{
    None,
    Loading,
    Ok,
    Error,
    BlobUnsupported,
    ImageOnly
}
