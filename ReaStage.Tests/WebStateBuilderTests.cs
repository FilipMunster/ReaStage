using ReaStage.Core;
using ReaStage.Services;

namespace ReaStage.Tests;

public class WebStateBuilderTests
{
    private static readonly List<ResolvedPlaylistItem> Items =
    [
        new ResolvedPlaylistItem(new PlaylistItem { RegionId = 1 }, new ReaperRegion(1, "One", 0, 100, null)),
        new ResolvedPlaylistItem(new PlaylistItem { RegionId = 2 }, new ReaperRegion(2, "Two", 110, 200, null)),
        new ResolvedPlaylistItem(new PlaylistItem { RegionId = 3 }, new ReaperRegion(3, "Three", 210, 300, null))
    ];

    private static readonly DateTime Now = new DateTime(2026, 1, 1, 20, 0, 0);

    private static ReaperPosition Pos(double seconds, ReaperPlayState state = ReaperPlayState.Playing, double tempo = 120)
    {
        return new ReaperPosition(state, seconds, 0, 0, 0, 4, 4, false, string.Empty, string.Empty, tempo);
    }

    [Fact]
    public void Build_InsideSong_FillsCurrentSongFields()
    {
        WebState state = WebStateBuilder.Build(Items, 1, Pos(150), true, Now);

        Assert.Equal(3, state.Songs.Count);
        Assert.Equal("Two", state.Songs[1].Name);
        Assert.Equal(1, state.CurrentIndex);
        Assert.True(state.IsPlaying);
        Assert.Equal("2/3", state.Order);
        Assert.Equal("0:40", state.Elapsed);
        Assert.Equal("−0:50", state.Remaining);
        // 50 s left of Two + 90 s of Three = 2:20
        Assert.Equal("2:20", state.RemainingPlaylist);
        Assert.Equal("20:02", state.EndEstimate);
    }

    [Fact]
    public void Build_BetweenSongs_HasNoCurrentAndCountsSongsAhead()
    {
        WebState state = WebStateBuilder.Build(Items, -1, Pos(105), true, Now);

        Assert.Equal(-1, state.CurrentIndex);
        Assert.Equal(0, state.Progress);
        Assert.Equal(string.Empty, state.Elapsed);
        Assert.Equal("–/3", state.Order);
        Assert.Equal("3:00", state.RemainingPlaylist);
    }
}
