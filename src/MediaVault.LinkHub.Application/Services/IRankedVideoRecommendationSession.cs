namespace MediaVault.LinkHub.Application.Services;

/// <summary>
/// Estado en memoria de la recomendación por ranking durante la vida del proceso.
/// Se reinicia al cerrar/abrir la aplicación (registro singleton).
/// </summary>
public interface IRankedVideoRecommendationSession
{
    IReadOnlyList<int> CurrentMediaFileIds { get; }

    IReadOnlyCollection<int> ShownMediaFileIds { get; }

    void SetCurrent(IReadOnlyList<int> mediaFileIds);

    void Reset();
}
