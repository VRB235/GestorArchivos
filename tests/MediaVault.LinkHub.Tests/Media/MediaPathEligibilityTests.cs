using FluentAssertions;
using MediaVault.LinkHub.Infrastructure.Media;

namespace MediaVault.LinkHub.Tests.Media;

public sealed class MediaPathEligibilityTests
{
    [Theory]
    [InlineData(@"D:\$RECYCLE.BIN\S-1-5\sticker.webm")]
    [InlineData(@"C:\System Volume Information\foo.mp4")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void IsUsableMediaPath_rejects_junk_and_empty(string? path) =>
        MediaPathEligibility.IsUsableMediaPath(path).Should().BeFalse();

    [Fact]
    public void IsUsableMediaPath_accepts_normal_vault_path() =>
        MediaPathEligibility.IsUsableMediaPath(@"D:\Vault\Actress\video.mp4").Should().BeTrue();

    [Fact]
    public void ExistsSafely_returns_false_for_recycle_bin_without_requiring_disk() =>
        MediaPathEligibility.ExistsSafely(@"D:\$RECYCLE.BIN\S-1-5\sticker.webm").Should().BeFalse();

    [Fact]
    public void IsUnderIndexRoot_accepts_paths_inside_root()
    {
        MediaPathEligibility.IsUnderIndexRoot(@"D:\Vault\A\clip.mp4", @"D:\Vault").Should().BeTrue();
        MediaPathEligibility.IsUnderIndexRoot(@"D:\Vault", @"D:\Vault").Should().BeTrue();
    }

    [Fact]
    public void IsUnderIndexRoot_rejects_paths_outside_root_and_recycle_bin()
    {
        MediaPathEligibility.IsUnderIndexRoot(@"D:\Other\clip.mp4", @"D:\Vault").Should().BeFalse();
        MediaPathEligibility.IsUnderIndexRoot(@"D:\$RECYCLE.BIN\x.webm", @"D:\Vault").Should().BeFalse();
    }
}
