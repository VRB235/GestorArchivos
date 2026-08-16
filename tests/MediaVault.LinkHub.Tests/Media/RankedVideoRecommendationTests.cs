using FluentAssertions;

using MediaVault.LinkHub.Application.Media;
using MediaVault.LinkHub.Application.Models.Dashboard;
using MediaVault.LinkHub.Application.Services;

namespace MediaVault.LinkHub.Tests.Media;

public sealed class RankedVideoRecommendationTests
{
    [Fact]
    public void PickByStarTiers_falls_back_to_unrated_when_no_ranked_videos()
    {
        var videos = new[]
        {
            CreateVideo(1, "sin.mp4", ranking: 0),
            CreateVideo(2, "foto.jpg", ranking: 5, isVideo: false)
        };

        var picked = RankedVideoRecommendation.PickByStarTiers(videos, excludeMediaFileIds: []);

        picked.Should().NotBeNull();
        picked!.Id.Should().Be(1);
    }

    [Fact]
    public void PickByStarTiers_prefers_five_star_tier_over_lower()
    {
        var fiveStars = new[]
        {
            CreateVideo(1, "a.mp4", ranking: 5),
            CreateVideo(2, "b.mp4", ranking: 5)
        };
        var mixed = fiveStars
            .Concat([CreateVideo(3, "c.mp4", ranking: 4), CreateVideo(4, "d.mp4", ranking: 3)])
            .ToArray();

        var random = new Random(7);
        for (var i = 0; i < 30; i++)
        {
            var picked = RankedVideoRecommendation.PickByStarTiers(mixed, [], random);
            picked.Should().NotBeNull();
            picked!.Id.Should().BeOneOf(1, 2);
        }
    }

    [Fact]
    public void PickByStarTiers_falls_to_four_stars_when_five_exhausted()
    {
        var videos = new[]
        {
            CreateVideo(1, "five-a.mp4", ranking: 5),
            CreateVideo(2, "five-b.mp4", ranking: 5),
            CreateVideo(3, "four.mp4", ranking: 4),
            CreateVideo(4, "three.mp4", ranking: 3)
        };

        var picked = RankedVideoRecommendation.PickByStarTiers(
            videos,
            excludeMediaFileIds: [1, 2],
            random: new Random(1));

        picked.Should().NotBeNull();
        picked!.Id.Should().Be(3);
        MediaFileRankingScale.ToDisplayStars(picked.RankingGlobal).Should().Be(4);
    }

    [Fact]
    public void PickByStarTiers_excludes_already_shown_within_same_tier()
    {
        var videos = new[]
        {
            CreateVideo(1, "a.mp4", ranking: 5),
            CreateVideo(2, "b.mp4", ranking: 5),
            CreateVideo(3, "c.mp4", ranking: 5)
        };

        var remaining = new HashSet<int> { 1, 2, 3 };
        var random = new Random(99);

        for (var i = 0; i < 3; i++)
        {
            var excluded = videos.Select(v => v.Id).Where(id => !remaining.Contains(id)).ToHashSet();
            var picked = RankedVideoRecommendation.PickByStarTiers(videos, excluded, random);
            picked.Should().NotBeNull();
            remaining.Should().Contain(picked!.Id);
            remaining.Remove(picked.Id);
        }

        RankedVideoRecommendation.PickByStarTiers(videos, [1, 2, 3], random)
            .Should().BeNull();
    }

    [Fact]
    public void PickByStarTiersMany_returns_up_to_five_preferring_higher_tiers()
    {
        var videos = new[]
        {
            CreateVideo(1, "five-a.mp4", ranking: 5),
            CreateVideo(2, "five-b.mp4", ranking: 5),
            CreateVideo(3, "five-c.mp4", ranking: 5),
            CreateVideo(4, "four.mp4", ranking: 4),
            CreateVideo(5, "three.mp4", ranking: 3),
            CreateVideo(6, "two.mp4", ranking: 2)
        };

        var picks = RankedVideoRecommendation.PickByStarTiersMany(
            videos,
            excludeMediaFileIds: [],
            count: 5,
            random: new Random(5));

        picks.Should().HaveCount(5);
        picks.Select(video => video.Id).Should().OnlyHaveUniqueItems();
        picks.Take(3).Select(video => video.Id).Should().BeSubsetOf([1, 2, 3]);
        picks[3].Id.Should().Be(4);
        picks[4].Id.Should().Be(5);
    }

    [Fact]
    public void PickByStarTiersMany_fills_to_five_when_few_ranked()
    {
        var videos = new[]
        {
            CreateVideo(1, "ranked.mp4", ranking: 3),
            CreateVideo(2, "a.mp4", ranking: 0),
            CreateVideo(3, "b.mp4", ranking: 0),
            CreateVideo(4, "c.mp4", ranking: 0),
            CreateVideo(5, "d.mp4", ranking: 0),
            CreateVideo(6, "e.mp4", ranking: 0)
        };

        var picks = RankedVideoRecommendation.PickByStarTiersMany(
            videos,
            excludeMediaFileIds: [],
            count: 5,
            random: new Random(2));

        picks.Should().HaveCount(5);
        picks[0].Id.Should().Be(1);
        picks.Select(video => video.Id).Should().OnlyHaveUniqueItems();
        picks.Skip(1).Should().OnlyContain(video => video.RankingGlobal <= 0);
    }

    [Fact]
    public void Session_tracks_shown_ids_and_resets()
    {
        var session = new RankedVideoRecommendationSession();
        session.SetCurrent([10, 20]);

        session.CurrentMediaFileIds.Should().Equal(10, 20);
        session.ShownMediaFileIds.Should().BeEquivalentTo([10, 20]);

        session.Reset();
        session.CurrentMediaFileIds.Should().BeEmpty();
        session.ShownMediaFileIds.Should().BeEmpty();
    }

    private static MediaFileViewStats CreateVideo(
        int id,
        string name,
        double ranking,
        bool isVideo = true) =>
        new()
        {
            Id = id,
            Name = name,
            Path = $@"C:\vault\{name}",
            Extension = Path.GetExtension(name),
            RankingGlobal = ranking,
            VecesAbierto = 0,
            IsVideo = isVideo
        };
}
