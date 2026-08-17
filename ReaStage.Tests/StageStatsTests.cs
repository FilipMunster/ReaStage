using ReaStage.Core;

namespace ReaStage.Tests;

public class StageStatsTests
{
    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(5, "0:05")]
    [InlineData(65, "1:05")]
    [InlineData(600, "10:00")]
    [InlineData(3661, "1:01:01")]
    [InlineData(-4, "0:00")]
    public void FormatClock_FormatsMinutesAndHours(double seconds, string expected)
    {
        Assert.Equal(expected, StageStats.FormatClock(seconds));
    }

    [Theory]
    [InlineData(4, 4, "4/4")]
    [InlineData(6, 8, "6/8")]
    [InlineData(0, 0, "")] // not reported by REAPER yet
    public void FormatTimeSignature_FormatsOrStaysEmpty(int numerator, int denominator, string expected)
    {
        Assert.Equal(expected, StageStats.FormatTimeSignature(numerator, denominator));
    }

    [Theory]
    [InlineData("586.4.64", 4)]
    [InlineData("1.1.00", 1)]
    [InlineData("12.3", 3)]
    public void BeatInBar_ReadsTheBeatComponent(string positionStringBeats, int expected)
    {
        Assert.Equal(expected, StageStats.BeatInBar(positionStringBeats));
    }

    [Theory]
    [InlineData("")]
    [InlineData("586")]
    [InlineData("nonsense")]
    public void BeatInBar_InvalidInput_ReturnsNull(string positionStringBeats)
    {
        Assert.Null(StageStats.BeatInBar(positionStringBeats));
    }

    [Theory]
    [InlineData(128, "128 BPM")]
    [InlineData(120.5, "120.5 BPM")]
    [InlineData(0, "— BPM")]
    public void FormatBpm_FormatsOrDashes(double tempo, string expected)
    {
        Assert.Equal(expected, StageStats.FormatBpm(tempo));
    }

    [Theory]
    [InlineData(0, 3, "1/3")]
    [InlineData(2, 3, "3/3")]
    [InlineData(-1, 3, "–/3")]
    public void OrderText_UsesOneBasedIndex(int index, int count, string expected)
    {
        Assert.Equal(expected, StageStats.OrderText(index, count));
    }

    private static readonly List<ResolvedPlaylistItem> Items =
    [
        new ResolvedPlaylistItem(new PlaylistItem { RegionId = 1 }, new ReaperRegion(1, "One", 0, 100, null)),
        new ResolvedPlaylistItem(new PlaylistItem { RegionId = 2 }, new ReaperRegion(2, "Two", 110, 200, null)),
        new ResolvedPlaylistItem(new PlaylistItem { RegionId = 3 }, new ReaperRegion(3, "Three", 210, 300, null))
    ];

    [Fact]
    public void RemainingPlaylistSeconds_InsideSong_AddsRestPlusFollowingLengths()
    {
        // 50 s left of song One (0..100) + 90 (Two) + 90 (Three)
        Assert.Equal(230, StageStats.RemainingPlaylistSeconds(Items, 0, 50));
    }

    [Fact]
    public void RemainingPlaylistSeconds_BetweenSongs_CountsSongsStillAhead()
    {
        // Position 105 is a gap: Two (90) + Three (90)
        Assert.Equal(180, StageStats.RemainingPlaylistSeconds(Items, -1, 105));
    }

    [Fact]
    public void FormatEndEstimate_AddsRemainingToNow()
    {
        DateTime now = new DateTime(2026, 1, 1, 20, 0, 0);
        Assert.Equal("21:01", StageStats.FormatEndEstimate(now, 3661));
    }
}
