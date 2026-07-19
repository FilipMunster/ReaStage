using ReaStage.Core;

namespace ReaStage.Tests;

public class PlaylistResolverTests
{
    private static ReaperRegion Region(int id, string name, double start = 0, double end = 10)
    {
        return new ReaperRegion(id, name, start, end, null);
    }

    private static Playlist PlaylistOf(params int[] regionIds)
    {
        return new Playlist
        {
            Name = "Test",
            Items = regionIds.Select(id => new PlaylistItem { RegionId = id }).ToList()
        };
    }

    [Fact]
    public void Resolve_MapsRegionsById()
    {
        Playlist playlist = PlaylistOf(2, 1);
        List<ReaperRegion> regions = [Region(1, "One"), Region(2, "Two")];

        List<ResolvedPlaylistItem> resolved = PlaylistResolver.Resolve(playlist, regions);

        Assert.Equal(2, resolved.Count);
        Assert.Equal("Two", resolved[0].Region!.Value.Name);
        Assert.Equal("One", resolved[1].Region!.Value.Name);
        Assert.All(resolved, r => Assert.False(r.IsMissing));
    }

    [Fact]
    public void Resolve_MissingRegion_IsMissing()
    {
        Playlist playlist = PlaylistOf(1, 99);
        List<ReaperRegion> regions = [Region(1, "One")];

        List<ResolvedPlaylistItem> resolved = PlaylistResolver.Resolve(playlist, regions);

        Assert.False(resolved[0].IsMissing);
        Assert.True(resolved[1].IsMissing);
        Assert.Null(resolved[1].Region);
    }

    [Fact]
    public void Resolve_PreservesPlaylistOrderIncludingDeleted()
    {
        Playlist playlist = PlaylistOf(3, 1, 2);
        playlist.Items[1].Deleted = true;
        List<ReaperRegion> regions = [Region(1, "One"), Region(2, "Two"), Region(3, "Three")];

        List<ResolvedPlaylistItem> resolved = PlaylistResolver.Resolve(playlist, regions);

        Assert.Equal([3, 1, 2], resolved.Select(r => r.Item.RegionId));
        Assert.True(resolved[1].Item.Deleted);
    }

    [Fact]
    public void Resolve_EmptyPlaylist_ReturnsEmpty()
    {
        List<ResolvedPlaylistItem> resolved = PlaylistResolver.Resolve(PlaylistOf(), [Region(1, "One")]);

        Assert.Empty(resolved);
    }
}
