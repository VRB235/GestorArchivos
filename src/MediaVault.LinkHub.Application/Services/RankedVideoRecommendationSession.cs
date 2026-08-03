namespace MediaVault.LinkHub.Application.Services;

public sealed class RankedVideoRecommendationSession : IRankedVideoRecommendationSession
{
    private readonly HashSet<int> _shownMediaFileIds = [];

    public int? CurrentMediaFileId { get; private set; }

    public IReadOnlyCollection<int> ShownMediaFileIds => _shownMediaFileIds;

    public void SetCurrent(int mediaFileId)
    {
        CurrentMediaFileId = mediaFileId;
        _shownMediaFileIds.Add(mediaFileId);
    }

    public void Reset()
    {
        CurrentMediaFileId = null;
        _shownMediaFileIds.Clear();
    }
}
