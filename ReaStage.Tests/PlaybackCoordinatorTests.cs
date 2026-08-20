using Microsoft.Extensions.Logging.Abstractions;
using ReaStage.Core;
using ReaStage.Services;

namespace ReaStage.Tests;

public class PlaybackCoordinatorTests
{
    private sealed class FakeReaperClient : IReaperClient
    {
        public List<string> Commands { get; } = [];

        // Simulates the HTTP TRANSPORT query; null = REAPER unreachable
        public ReaperPosition? PositionToReturn { get; set; }

        public event EventHandler<ReaperPositionChangedEventArgs>? PositionChanged;

        public Task<ReaperPosition> GetPosition()
        {
            return PositionToReturn is ReaperPosition position
                ? Task.FromResult(position)
                : throw new NotSupportedException();
        }

        public Task<List<ReaperRegion>> GetRegions() => throw new NotSupportedException();

        public Task SendPlay()
        {
            Commands.Add("play");
            return Task.CompletedTask;
        }

        public Task SendPlayPause()
        {
            Commands.Add("playpause");
            return Task.CompletedTask;
        }

        public Task SendStop()
        {
            Commands.Add("stop");
            return Task.CompletedTask;
        }

        public Task SetPosition(double seconds)
        {
            Commands.Add($"setpos:{seconds}");
            return Task.CompletedTask;
        }

        public Task StopAndSetPosition(double seconds)
        {
            Commands.Add($"stopset:{seconds}");
            return Task.CompletedTask;
        }

        public void RaisePosition(ReaperPosition position)
        {
            PositionChanged?.Invoke(this, new ReaperPositionChangedEventArgs(position));
        }
    }

    private sealed class FakeRegionCatalog : IRegionCatalog
    {
        public event EventHandler? RegionsChanged;

        // The coordinator does not consume reachability
        public event EventHandler? ReachabilityChanged
        {
            add
            {
            }
            remove
            {
            }
        }

        public IReadOnlyList<ReaperRegion> Regions { get; private set; } = [];

        public bool IsReaperReachable => true;

        public Task RefreshAsync() => Task.CompletedTask;

        public void SetRegions(IReadOnlyList<ReaperRegion> regions)
        {
            Regions = regions;
            RegionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakePlaylistService : IPlaylistService
    {
        public event EventHandler? PlaylistsChanged;

        public List<Playlist> PlaylistsList { get; } = [];

        public IReadOnlyList<Playlist> Playlists => PlaylistsList;

        public Guid? ActivePlaylistId { get; set; }

        public Playlist? GetPlaylist(Guid id) => PlaylistsList.FirstOrDefault(p => p.Id == id);

        public string CreateRestorePoint() => throw new NotSupportedException();
        public void Restore(string restorePoint) => throw new NotSupportedException();

        public Playlist CreatePlaylist(string name, IEnumerable<int> regionIds) => throw new NotSupportedException();
        public Playlist DuplicatePlaylist(Guid id) => throw new NotSupportedException();
        public void DeletePlaylist(Guid id) => throw new NotSupportedException();
        public void RenamePlaylist(Guid id, string name) => throw new NotSupportedException();
        public void MoveItem(Guid playlistId, int fromIndex, int toIndex) => throw new NotSupportedException();
        public void SetItemDeleted(Guid playlistId, int itemIndex, bool deleted) => throw new NotSupportedException();

        public void RaiseChanged()
        {
            PlaylistsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public event EventHandler? SettingsSaved;

        public AppSettings Settings { get; } = new();

        public void Save()
        {
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
    }

    private readonly FakeReaperClient client = new();
    private readonly FakeRegionCatalog catalog = new();
    private readonly FakePlaylistService playlists = new();
    private readonly FakeSettingsService settings = new();

    // Timeline: three songs with silence gaps between them
    private static readonly List<ReaperRegion> DefaultRegions =
    [
        new ReaperRegion(1, "One", 0, 100, null),
        new ReaperRegion(2, "Two", 110, 200, null),
        new ReaperRegion(3, "Three", 210, 300, null)
    ];

    private PlaybackCoordinator CreateCoordinator(IReadOnlyList<ReaperRegion>? regions = null)
    {
        catalog.SetRegions(regions ?? DefaultRegions);
        return new PlaybackCoordinator(client, catalog, playlists, settings, NullLogger<PlaybackCoordinator>.Instance);
    }

    private static ReaperPosition Pos(double seconds, ReaperPlayState state = ReaperPlayState.Playing, double tempo = 0)
    {
        // Time signature 4/4; tempo 120 BPM -> 2 s per bar
        return new ReaperPosition(state, seconds, 0, 0, 0, 4, 4, false, $"{seconds}", string.Empty, tempo);
    }

    [Fact]
    public void VirtualPlaylist_UsesRegionsInTimelineOrder()
    {
        List<ReaperRegion> unordered =
        [
            new ReaperRegion(2, "Two", 110, 200, null),
            new ReaperRegion(1, "One", 0, 100, null)
        ];

        PlaybackCoordinator coordinator = CreateCoordinator(unordered);

        Assert.Equal(["One", "Two"], coordinator.ActiveItems.Select(i => i.Region!.Value.Name));
    }

    [Fact]
    public void PositionInsideSong_SetsCurrentIndex()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();

        client.RaisePosition(Pos(150));

        Assert.Equal(1, coordinator.CurrentIndex);
    }

    [Fact]
    public void PositionInGap_CurrentIndexIsMinusOne()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();

        client.RaisePosition(Pos(105));

        Assert.Equal(-1, coordinator.CurrentIndex);
    }

    [Fact]
    public void SongEnd_CrossedWhilePlaying_StopsAndPreparesNextSong()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();
        client.RaisePosition(Pos(99));

        client.RaisePosition(Pos(100.5));

        Assert.Equal(["stopset:110"], client.Commands);
    }

    [Fact]
    public void SongEnd_LastSong_JustStops()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();
        client.RaisePosition(Pos(299));

        client.RaisePosition(Pos(300.5));

        Assert.Equal(["stop"], client.Commands);
    }

    [Fact]
    public void SongEnd_NotCrossedWhenStopped()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();
        client.RaisePosition(Pos(99, ReaperPlayState.Stopped));

        client.RaisePosition(Pos(100.5, ReaperPlayState.Stopped));

        Assert.Empty(client.Commands);
    }

    [Fact]
    public void SongEnd_LargeForwardJump_IsManualSeekNotSongEnd()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();
        client.RaisePosition(Pos(50));

        client.RaisePosition(Pos(150));

        Assert.Empty(client.Commands);
        Assert.Equal(1, coordinator.CurrentIndex);
    }

    [Fact]
    public async Task GoToNext_JumpsToStartOfNextSong()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();
        client.RaisePosition(Pos(50));

        await coordinator.GoToNextAsync();

        Assert.Equal(["stopset:110"], client.Commands);
    }

    [Fact]
    public async Task GoToNext_OnLastSong_DoesNothing()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();
        client.RaisePosition(Pos(250));

        await coordinator.GoToNextAsync();

        Assert.Empty(client.Commands);
    }

    [Fact]
    public async Task GoToNext_NoCurrentSong_JumpsToFirst()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();
        client.RaisePosition(Pos(105));

        await coordinator.GoToNextAsync();

        Assert.Equal(["stopset:0"], client.Commands);
    }

    [Fact]
    public async Task GoToPrevious_BeyondThreshold_JumpsToCurrentSongStart()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();
        client.RaisePosition(Pos(150));

        await coordinator.GoToPreviousAsync();

        Assert.Equal(["stopset:110"], client.Commands);
    }

    [Fact]
    public async Task GoToPrevious_WithinThreshold_JumpsToPreviousSong()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();
        client.RaisePosition(Pos(111));

        await coordinator.GoToPreviousAsync();

        Assert.Equal(["stopset:0"], client.Commands);
    }

    [Fact]
    public async Task GoToPrevious_FirstSongWithinThreshold_StaysOnFirst()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();
        client.RaisePosition(Pos(1));

        await coordinator.GoToPreviousAsync();

        Assert.Equal(["stopset:0"], client.Commands);
    }

    [Fact]
    public async Task GoToPrevious_NoCurrentSong_JumpsToFirst()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();
        client.RaisePosition(Pos(105));

        await coordinator.GoToPreviousAsync();

        Assert.Equal(["stopset:0"], client.Commands);
    }

    [Fact]
    public async Task StoredPlaylist_NavigationFollowsPlaylistOrderNotTimeline()
    {
        Playlist playlist = new Playlist
        {
            Name = "Custom",
            Items = [new PlaylistItem { RegionId = 3 }, new PlaylistItem { RegionId = 1 }]
        };
        playlists.PlaylistsList.Add(playlist);
        playlists.ActivePlaylistId = playlist.Id;

        PlaybackCoordinator coordinator = CreateCoordinator();
        client.RaisePosition(Pos(250)); // inside song 3, first item of the playlist

        await coordinator.GoToNextAsync();

        // Next in playlist is region 1 (timeline start 0), not region 3's timeline successor
        Assert.Equal(["stopset:0"], client.Commands);
    }

    [Fact]
    public void StoredPlaylist_DeletedAndMissingItemsAreExcluded()
    {
        Playlist playlist = new Playlist
        {
            Name = "Custom",
            Items =
            [
                new PlaylistItem { RegionId = 1 },
                new PlaylistItem { RegionId = 2, Deleted = true },
                new PlaylistItem { RegionId = 99 }
            ]
        };
        playlists.PlaylistsList.Add(playlist);
        playlists.ActivePlaylistId = playlist.Id;

        PlaybackCoordinator coordinator = CreateCoordinator();

        Assert.Single(coordinator.ActiveItems);
        Assert.Equal("One", coordinator.ActiveItems[0].Region!.Value.Name);
    }

    [Fact]
    public void RegionsChanged_RebuildsActiveItems()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();
        Assert.Equal(3, coordinator.ActiveItems.Count);

        catalog.SetRegions([new ReaperRegion(1, "Only", 0, 50, null)]);

        Assert.Single(coordinator.ActiveItems);
    }

    [Fact]
    public async Task GoToNext_WhileStopped_RepeatedPressesAdvanceThroughPlaylist()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();
        client.RaisePosition(Pos(50, ReaperPlayState.Stopped));

        client.PositionToReturn = Pos(110, ReaperPlayState.Stopped);
        await coordinator.GoToNextAsync();

        client.PositionToReturn = Pos(210, ReaperPlayState.Stopped);
        await coordinator.GoToNextAsync();

        Assert.Equal(["stopset:110", "stopset:210"], client.Commands);
        Assert.Equal(2, coordinator.CurrentIndex);
    }

    [Fact]
    public async Task GoToPrevious_WhileStopped_RepeatedPressesWalkBack()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();
        client.RaisePosition(Pos(211, ReaperPlayState.Stopped));

        client.PositionToReturn = Pos(110, ReaperPlayState.Stopped);
        await coordinator.GoToPreviousAsync();

        client.PositionToReturn = Pos(0, ReaperPlayState.Stopped);
        await coordinator.GoToPreviousAsync();

        Assert.Equal(["stopset:110", "stopset:0"], client.Commands);
        Assert.Equal(0, coordinator.CurrentIndex);
    }

    [Fact]
    public void SongEnd_AfterAutoAdvance_CurrentIndexPointsToPreparedSong()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();
        client.RaisePosition(Pos(99));

        client.PositionToReturn = Pos(110, ReaperPlayState.Stopped);
        client.RaisePosition(Pos(100.5));

        Assert.Equal(["stopset:110"], client.Commands);
        Assert.Equal(1, coordinator.CurrentIndex);
    }

    [Fact]
    public async Task TogglePlayPause_SendsPlayPause()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();

        await coordinator.TogglePlayPauseAsync();

        Assert.Equal(["playpause"], client.Commands);
    }

    [Fact]
    public async Task JumpToItemAt_InRange_StopsAndSeeksToSongStart()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();

        await coordinator.JumpToItemAtAsync(2);

        Assert.Equal(["stopset:210"], client.Commands);
    }

    [Fact]
    public async Task JumpToItemAt_OutOfRange_DoesNothing()
    {
        PlaybackCoordinator coordinator = CreateCoordinator();

        await coordinator.JumpToItemAtAsync(5);

        Assert.Empty(client.Commands);
    }
}
