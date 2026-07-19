using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ReaStage.Core;
using ReaStage.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ReaStage.ViewModels;

public partial class StageViewModel : ViewModelBase
{
    private readonly IPlaybackCoordinator playback;
    private readonly IRegionCatalog regionCatalog;
    private readonly ISettingsService settingsService;

    [ObservableProperty]
    private IReadOnlyList<string> previousSongs = [];

    [ObservableProperty]
    private IReadOnlyList<string> nextSongs = [];

    [ObservableProperty]
    private string currentSongName = "—";

    [ObservableProperty]
    private bool hasCurrentSong;

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private bool isPlaying;

    [ObservableProperty]
    private string positionText = string.Empty;

    [ObservableProperty]
    private bool isReaperReachable;

    public StageViewModel(
        IPlaybackCoordinator playback,
        IRegionCatalog regionCatalog,
        ISettingsService settingsService)
    {
        this.playback = playback;
        this.regionCatalog = regionCatalog;
        this.settingsService = settingsService;

        playback.StateChanged += (_, _) => Dispatcher.UIThread.Post(Update);
        regionCatalog.ReachabilityChanged += (_, _) => Dispatcher.UIThread.Post(Update);
        Update();
    }

    private void Update()
    {
        IReadOnlyList<ResolvedPlaylistItem> items = playback.ActiveItems;
        int index = playback.CurrentIndex;
        ReaperPosition position = playback.Position;
        AppSettings settings = settingsService.Settings;

        if (index >= 0)
        {
            ReaperRegion current = items[index].Region!.Value;

            HasCurrentSong = true;
            CurrentSongName = current.Name;
            Progress = ComputeProgress(position.PositionSeconds, current);

            int previousCount = Math.Min(settings.PreviousSongsShown, index);
            PreviousSongs = items.Skip(index - previousCount).Take(previousCount).Select(SongName).ToList();
            NextSongs = items.Skip(index + 1).Take(settings.NextSongsShown).Select(SongName).ToList();
        }
        else
        {
            // Between songs (or end of playlist): no progress, upcoming songs from the playlist start
            HasCurrentSong = false;
            CurrentSongName = "—";
            Progress = 0;
            PreviousSongs = [];
            NextSongs = items.Take(settings.NextSongsShown).Select(SongName).ToList();
        }

        IsPlaying = position.PlayState is ReaperPlayState.Playing or ReaperPlayState.Recording;
        PositionText = position.PositionString;
        IsReaperReachable = regionCatalog.IsReaperReachable;
    }

    private static string SongName(ResolvedPlaylistItem item)
    {
        return item.Region!.Value.Name;
    }

    private static double ComputeProgress(double seconds, ReaperRegion region)
    {
        double length = region.EndPosition - region.StartPosition;
        if (length <= 0)
        {
            return 0;
        }

        return Math.Clamp((seconds - region.StartPosition) / length, 0, 1);
    }
}
