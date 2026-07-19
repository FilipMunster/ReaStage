using Microsoft.Extensions.Logging;
using ReaStage.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ReaStage.Services;

internal class PlaybackCoordinator : IPlaybackCoordinator, IDisposable
{
    // A song counts as "ended" only when the position lands this close past its end;
    // larger forward jumps are treated as manual seeks
    private const double EndCrossingSlackSeconds = 2.0;

    private readonly IReaperClient client;
    private readonly IRegionCatalog regionCatalog;
    private readonly IPlaylistService playlistService;
    private readonly ISettingsService settingsService;
    private readonly ILogger<PlaybackCoordinator> logger;

    public event EventHandler? StateChanged;

    public IReadOnlyList<ResolvedPlaylistItem> ActiveItems { get; private set; } = [];

    public int CurrentIndex { get; private set; } = -1;

    public ReaperPosition Position { get; private set; }

    public PlaybackCoordinator(
        IReaperClient client,
        IRegionCatalog regionCatalog,
        IPlaylistService playlistService,
        ISettingsService settingsService,
        ILogger<PlaybackCoordinator> logger)
    {
        this.client = client;
        this.regionCatalog = regionCatalog;
        this.playlistService = playlistService;
        this.settingsService = settingsService;
        this.logger = logger;

        client.PositionChanged += Client_PositionChanged;
        regionCatalog.RegionsChanged += (_, _) => RebuildActiveItems();
        playlistService.PlaylistsChanged += (_, _) => RebuildActiveItems();

        RebuildActiveItems();

        // REAPER only streams OSC while playing — fetch the initial state over HTTP
        _ = RefreshPositionAsync();
    }

    public async Task TogglePlayPauseAsync()
    {
        try
        {
            await client.SendPlayPause();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Play/Pause to REAPER");
        }
    }

    public async Task GoToNextAsync()
    {
        int target = CurrentIndex >= 0 ? CurrentIndex + 1 : 0;
        if (target >= ActiveItems.Count)
        {
            return;
        }

        await JumpToItemAsync(target);
    }

    public async Task GoToPreviousAsync()
    {
        if (ActiveItems.Count == 0)
        {
            return;
        }

        int target = 0;
        if (CurrentIndex >= 0)
        {
            double intoSong = Position.PositionSeconds - ActiveItems[CurrentIndex].Region!.Value.StartPosition;
            target = intoSong > settingsService.Settings.PreviousThresholdSeconds
                ? CurrentIndex
                : Math.Max(0, CurrentIndex - 1);
        }

        await JumpToItemAsync(target);
    }

    private async Task JumpToItemAsync(int index)
    {
        try
        {
            await client.StopAndSetPosition(ActiveItems[index].Region!.Value.StartPosition);
            logger.LogInformation("Jumped to song {Index}: {Name}", index, ActiveItems[index].Region!.Value.Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to jump to song {Index}", index);
            return;
        }

        // While stopped REAPER sends no OSC updates, so the new position must be fetched
        await RefreshPositionAsync();
    }

    private async Task RefreshPositionAsync()
    {
        try
        {
            ReaperPosition position = await client.GetPosition();
            Position = position;
            CurrentIndex = FindItemIndex(position.PositionSeconds);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh position from REAPER");
        }
    }

    private void Client_PositionChanged(object? sender, ReaperPositionChangedEventArgs e)
    {
        ReaperPosition previous = Position;
        int previousIndex = CurrentIndex;

        Position = e.Position;
        CurrentIndex = FindItemIndex(e.Position.PositionSeconds);

        if (previousIndex >= 0 && previous.PlayState == ReaperPlayState.Playing)
        {
            CheckSongEnded(previousIndex, previous.PositionSeconds, e.Position.PositionSeconds);
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CheckSongEnded(int songIndex, double previousSeconds, double currentSeconds)
    {
        double songEnd = ActiveItems[songIndex].Region!.Value.EndPosition;
        bool crossedEnd = previousSeconds < songEnd
            && currentSeconds >= songEnd
            && currentSeconds < songEnd + EndCrossingSlackSeconds;

        if (!crossedEnd)
        {
            return;
        }

        // Stop and prepare the next song; the silence between songs in the REAPER
        // project covers the detection delay
        _ = HandleSongEndedAsync(songIndex);
    }

    private async Task HandleSongEndedAsync(int songIndex)
    {
        try
        {
            int next = songIndex + 1;
            if (next < ActiveItems.Count)
            {
                await client.StopAndSetPosition(ActiveItems[next].Region!.Value.StartPosition);
                logger.LogInformation("Song ended, prepared next song: {Name}", ActiveItems[next].Region!.Value.Name);
            }
            else
            {
                await client.SendStop();
                logger.LogInformation("Last song of the playlist ended, stopped");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle song end");
            return;
        }

        await RefreshPositionAsync();
    }

    private void RebuildActiveItems()
    {
        IReadOnlyList<ReaperRegion> regions = regionCatalog.Regions;
        Guid? activeId = playlistService.ActivePlaylistId;

        List<ResolvedPlaylistItem> items;
        if (activeId is null)
        {
            // Virtual REAPER playlist: regions in timeline order
            items = regions
                .OrderBy(r => r.StartPosition)
                .Select(r => new ResolvedPlaylistItem(new PlaylistItem { RegionId = r.Id }, r))
                .ToList();
        }
        else
        {
            Playlist? playlist = playlistService.GetPlaylist(activeId.Value);
            items = playlist is null
                ? []
                : PlaylistResolver.Resolve(playlist, regions)
                    .Where(i => !i.Item.Deleted && !i.IsMissing)
                    .ToList();
        }

        ActiveItems = items;
        CurrentIndex = FindItemIndex(Position.PositionSeconds);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private int FindItemIndex(double seconds)
    {
        IReadOnlyList<ResolvedPlaylistItem> items = ActiveItems;
        for (int i = 0; i < items.Count; i++)
        {
            ReaperRegion region = items[i].Region!.Value;
            if (seconds >= region.StartPosition && seconds < region.EndPosition)
            {
                return i;
            }
        }

        return -1;
    }

    public void Dispose()
    {
        client.PositionChanged -= Client_PositionChanged;
    }
}
