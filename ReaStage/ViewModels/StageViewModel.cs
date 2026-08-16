using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReaStage.Core;
using ReaStage.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ReaStage.ViewModels;

public partial class StageViewModel : ViewModelBase
{
    // How long an "armed" song stays armed before the second click must confirm
    private static readonly TimeSpan ArmTimeout = TimeSpan.FromSeconds(4);

    private readonly IPlaybackCoordinator playback;
    private readonly IRegionCatalog regionCatalog;
    private readonly ISettingsService settingsService;
    private readonly DispatcherTimer armTimer;
    private readonly DispatcherTimer endEstimateTimer;
    private int? armedIndex;

    [ObservableProperty]
    private IReadOnlyList<StageSongItem> previousSongs = [];

    [ObservableProperty]
    private IReadOnlyList<StageSongItem> nextSongs = [];

    [ObservableProperty]
    private string currentSongName = "—";

    [ObservableProperty]
    private bool hasCurrentSong;

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private bool isPlaying;

    // Times in the current song card: elapsed (left) and remaining (right),
    // each shown as both mm:ss and bar.beat
    [ObservableProperty]
    private string elapsedText = string.Empty;

    [ObservableProperty]
    private string elapsedBeatsText = string.Empty;

    [ObservableProperty]
    private string remainingText = string.Empty;

    [ObservableProperty]
    private string remainingBeatsText = string.Empty;

    // Bottom status bar
    [ObservableProperty]
    private string bpmText = string.Empty;

    [ObservableProperty]
    private string timeSignatureText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<MetronomeDot> metronomeDots = [];

    [ObservableProperty]
    private bool showMetronome = true;

    // Song order split so the view can put a rule between the two numbers
    [ObservableProperty]
    private string songNumberText = string.Empty;

    [ObservableProperty]
    private string songCountText = string.Empty;

    [ObservableProperty]
    private string remainingPlaylistText = string.Empty;

    [ObservableProperty]
    private string endEstimateText = string.Empty;

    [ObservableProperty]
    private bool isReaperReachable;

    // Font size multiplier for the stage view, applied via FontScaleConverter
    [ObservableProperty]
    private double fontScale = 1.0;

    private double remainingPlaylistSeconds;

    public StageViewModel(
        IPlaybackCoordinator playback,
        IRegionCatalog regionCatalog,
        ISettingsService settingsService)
    {
        this.playback = playback;
        this.regionCatalog = regionCatalog;
        this.settingsService = settingsService;

        armTimer = new DispatcherTimer { Interval = ArmTimeout };
        armTimer.Tick += (_, _) => ClearArm();

        // The end-of-playlist estimate drifts with wall-clock time even while stopped
        endEstimateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        endEstimateTimer.Tick += (_, _) => UpdateEndEstimate();
        endEstimateTimer.Start();

        playback.StateChanged += (_, _) => Dispatcher.UIThread.Post(Update);
        regionCatalog.ReachabilityChanged += (_, _) => Dispatcher.UIThread.Post(Update);
        settingsService.SettingsSaved += (_, _) => Dispatcher.UIThread.Post(OnSettingsSaved);

        ApplyDisplaySettings();
        Update();
    }

    private void OnSettingsSaved()
    {
        ApplyDisplaySettings();
        Update();
    }

    private void ApplyDisplaySettings()
    {
        AppSettings settings = settingsService.Settings;
        FontScale = Math.Clamp(settings.FontScalePercent, 50, 200) / 100.0;
        ShowMetronome = settings.ShowMetronome;
    }

    [RelayCommand]
    private Task PlayPause() => playback.TogglePlayPauseAsync();

    [RelayCommand]
    private Task Previous() => playback.GoToPreviousAsync();

    [RelayCommand]
    private Task Next() => playback.GoToNextAsync();

    // Two-click jump: first click arms the song, second within the timeout confirms
    [RelayCommand]
    private async Task ArmOrJump(StageSongItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (armedIndex == item.Index)
        {
            ClearArm();
            await playback.JumpToItemAtAsync(item.Index);
            return;
        }

        armedIndex = item.Index;
        ApplyArmedFlag();
        armTimer.Stop();
        armTimer.Start();
    }

    // Returns true when an armed song was cleared (e.g. so Escape can consume the key)
    public bool ClearArm()
    {
        if (armedIndex is null)
        {
            return false;
        }

        armedIndex = null;
        armTimer.Stop();
        ApplyArmedFlag();
        return true;
    }

    private void ApplyArmedFlag()
    {
        foreach (StageSongItem item in PreviousSongs.Concat(NextSongs))
        {
            item.IsArmed = item.Index == armedIndex;
        }
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
            UpdateSongTimes(current, position);

            int previousCount = Math.Min(settings.PreviousSongsShown, index);
            int nextCount = Math.Min(settings.NextSongsShown, items.Count - index - 1);
            PreviousSongs = KeepOrBuild(PreviousSongs, items, index - previousCount, previousCount);
            NextSongs = KeepOrBuild(NextSongs, items, index + 1, nextCount);
        }
        else
        {
            // Between songs (or end of playlist): no progress, upcoming songs from the playlist start
            HasCurrentSong = false;
            CurrentSongName = "—";
            Progress = 0;
            ClearSongTimes();
            PreviousSongs = PreviousSongs.Count == 0 ? PreviousSongs : [];
            NextSongs = KeepOrBuild(NextSongs, items, 0, Math.Min(settings.NextSongsShown, items.Count));
        }

        BpmText = StageStats.FormatBpm(position.TempoBpm);
        TimeSignatureText = StageStats.FormatTimeSignature(position.TimeSigNumerator, position.TimeSigDenominator);
        UpdateMetronome(position);
        SongNumberText = index >= 0 ? (index + 1).ToString(CultureInfo.InvariantCulture) : "–";
        SongCountText = items.Count.ToString(CultureInfo.InvariantCulture);
        remainingPlaylistSeconds = StageStats.RemainingPlaylistSeconds(items, index, position.PositionSeconds);
        RemainingPlaylistText = StageStats.FormatClock(remainingPlaylistSeconds);
        UpdateEndEstimate();

        // A rebuild replaces the item instances, so re-apply (or drop) the armed state
        if (armedIndex is int armed && PreviousSongs.Concat(NextSongs).All(i => i.Index != armed))
        {
            armedIndex = null;
            armTimer.Stop();
        }
        ApplyArmedFlag();

        IsPlaying = position.PlayState is ReaperPlayState.Playing or ReaperPlayState.Recording;
        IsReaperReachable = regionCatalog.IsReaperReachable;
    }

    private void UpdateSongTimes(ReaperRegion current, ReaperPosition position)
    {
        double elapsed = Math.Max(0, position.PositionSeconds - current.StartPosition);
        double remaining = Math.Max(0, current.EndPosition - position.PositionSeconds);

        ElapsedText = StageStats.FormatClock(elapsed);
        RemainingText = "−" + StageStats.FormatClock(remaining);

        string? elapsedBeats = StageStats.FormatBarBeatPosition(elapsed, position.TempoBpm, position.TimeSigNumerator, position.TimeSigDenominator);
        string? remainingBeats = StageStats.FormatBarBeatDuration(remaining, position.TempoBpm, position.TimeSigNumerator, position.TimeSigDenominator);
        ElapsedBeatsText = elapsedBeats ?? string.Empty;
        RemainingBeatsText = remainingBeats is null ? string.Empty : "−" + remainingBeats;
    }

    // The dots follow REAPER's reported beat directly — no local clock, no smoothing,
    // so they stay in step with the audio metronome as closely as OSC allows
    private void UpdateMetronome(ReaperPosition position)
    {
        int beatsPerBar = position.TimeSigNumerator;
        if (beatsPerBar <= 0)
        {
            if (MetronomeDots.Count > 0)
            {
                MetronomeDots = [];
            }

            return;
        }

        // Rebuild only when the bar length changes
        if (MetronomeDots.Count != beatsPerBar)
        {
            MetronomeDots = Enumerable.Range(0, beatsPerBar).Select(i => new MetronomeDot(i == 0)).ToList();
        }

        bool playing = position.PlayState is ReaperPlayState.Playing or ReaperPlayState.Recording;
        int? beat = playing ? StageStats.BeatInBar(position.PositionStringBeats) : null;

        // Variant A: beats fill up through the bar and reset on the downbeat
        int litCount = beat is int b ? Math.Min(b, beatsPerBar) : 0;
        for (int i = 0; i < MetronomeDots.Count; i++)
        {
            MetronomeDots[i].IsLit = i < litCount;
        }
    }

    private void ClearSongTimes()
    {
        ElapsedText = string.Empty;
        RemainingText = string.Empty;
        ElapsedBeatsText = string.Empty;
        RemainingBeatsText = string.Empty;
    }

    private void UpdateEndEstimate()
    {
        EndEstimateText = StageStats.FormatEndEstimate(DateTime.Now, remainingPlaylistSeconds);
    }

    // Position updates arrive several times a second while playing. Handing the
    // ItemsControl a new collection every time makes it discard and recreate the
    // containers, which flickers the cursor and swallows clicks, so the existing
    // instances are kept whenever the songs on screen have not actually changed.
    private static IReadOnlyList<StageSongItem> KeepOrBuild(
        IReadOnlyList<StageSongItem> current,
        IReadOnlyList<ResolvedPlaylistItem> items,
        int start,
        int count)
    {
        if (current.Count == count)
        {
            bool unchanged = true;
            for (int i = 0; i < count; i++)
            {
                if (current[i].Index != start + i || current[i].Name != SongName(items[start + i]))
                {
                    unchanged = false;
                    break;
                }
            }

            if (unchanged)
            {
                return current;
            }
        }

        return Enumerable.Range(start, count)
            .Select(i => new StageSongItem(i, SongName(items[i])))
            .ToList();
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
