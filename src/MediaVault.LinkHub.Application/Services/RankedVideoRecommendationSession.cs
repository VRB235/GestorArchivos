namespace MediaVault.LinkHub.Application.Services;

public sealed class RankedVideoRecommendationSession : IRankedVideoRecommendationSession
{
    private readonly HashSet<int> _shownMediaFileIds = [];
    private IReadOnlyList<int> _currentMediaFileIds = [];

    public IReadOnlyList<int> CurrentMediaFileIds => _currentMediaFileIds;

    public IReadOnlyCollection<int> ShownMediaFileIds => _shownMediaFileIds;

    public void SetCurrent(IReadOnlyList<int> mediaFileIds)
    {
        _currentMediaFileIds = mediaFileIds.Count == 0
            ? []
            : mediaFileIds.ToArray();

        foreach (var mediaFileId in _currentMediaFileIds)
            _shownMediaFileIds.Add(mediaFileId);
    }

    public void Reset()
    {
        _currentMediaFileIds = [];
        _shownMediaFileIds.Clear();
    }
}
