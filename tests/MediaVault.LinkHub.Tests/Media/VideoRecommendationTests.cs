using FluentAssertions;

using MediaVault.LinkHub.Application.Media;
using MediaVault.LinkHub.Application.Models.Dashboard;

namespace MediaVault.LinkHub.Tests.Media;

public sealed class VideoRecommendationTests
{
    [Fact]
    public void PickWeighted_returns_null_when_no_videos()
    {
        VideoRecommendation.PickWeighted([], excludeMediaFileId: null)
            .Should().BeNull();
    }

    [Fact]
    public void PickWeighted_returns_only_candidate_even_when_excluded()
    {
        var only = CreateVideo(1, "solo.mp4", ranking: 4, opens: 2);

        var picked = VideoRecommendation.PickWeighted([only], excludeMediaFileId: 1);

        picked.Should().BeSameAs(only);
    }

    [Fact]
    public void PickWeighted_excludes_current_id_when_multiple_candidates()
    {
        var videos = new[]
        {
            CreateVideo(1, "a.mp4", ranking: 5, opens: 10),
            CreateVideo(2, "b.mp4", ranking: 4, opens: 5),
            CreateVideo(3, "c.mp4", ranking: 3, opens: 1)
        };

        var random = new Random(42);
        for (var i = 0; i < 40; i++)
        {
            var picked = VideoRecommendation.PickWeighted(videos, excludeMediaFileId: 1, random);
            picked.Should().NotBeNull();
            picked!.Id.Should().NotBe(1);
        }
    }

    [Fact]
    public void ComputeScore_favors_higher_ranking_and_opens()
    {
        var low = CreateVideo(1, "low.mp4", ranking: 1, opens: 0);
        var high = CreateVideo(2, "high.mp4", ranking: 5, opens: 20);

        VideoRecommendation.ComputeScore(high, randomNoise: 0)
            .Should().BeGreaterThan(VideoRecommendation.ComputeScore(low, randomNoise: 0));
    }

    private static MediaFileViewStats CreateVideo(int id, string name, double ranking, int opens) =>
        new()
        {
            Id = id,
            Name = name,
            Path = $@"C:\vault\{name}",
            Extension = ".mp4",
            RankingGlobal = ranking,
            VecesAbierto = opens,
            IsVideo = true
        };
}
