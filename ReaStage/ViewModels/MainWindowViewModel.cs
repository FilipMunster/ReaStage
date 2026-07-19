using Avalonia.Input;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ReaStage.Core;
using ReaStage.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ReaStage.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IPlaybackCoordinator playback;
    private readonly IRegionCatalog regionCatalog;
    private readonly ILogger<MainWindowViewModel> logger;
    private readonly KeyGesture playPauseGesture;
    private readonly KeyGesture previousGesture;
    private readonly KeyGesture nextGesture;

    [ObservableProperty]
    private string reaperPosition = string.Empty;

    [ObservableProperty]
    private string currentSong = string.Empty;

    [ObservableProperty]
    private string regions = string.Empty;

    public MainWindowViewModel(
        IPlaybackCoordinator playback,
        IRegionCatalog regionCatalog,
        ISettingsService settingsService,
        ILogger<MainWindowViewModel> logger)
    {
        this.playback = playback;
        this.regionCatalog = regionCatalog;
        this.logger = logger;
        logger.LogDebug("MainWindowViewModel created");

        AppSettings.KeySettings keys = settingsService.Settings.Keys;
        playPauseGesture = ParseGesture(keys.PlayPause, Key.Space);
        previousGesture = ParseGesture(keys.Previous, Key.Left);
        nextGesture = ParseGesture(keys.Next, Key.Right);

        playback.StateChanged += Playback_StateChanged;
        regionCatalog.RegionsChanged += RegionCatalog_RegionsChanged;
        UpdateRegionsText();
    }

    // Returns true when the key was handled as a transport shortcut
    public bool HandleKey(KeyEventArgs e)
    {
        if (playPauseGesture.Matches(e))
        {
            PlayPauseCommand.Execute(null);
            return true;
        }

        if (previousGesture.Matches(e))
        {
            PreviousCommand.Execute(null);
            return true;
        }

        if (nextGesture.Matches(e))
        {
            NextCommand.Execute(null);
            return true;
        }

        return false;
    }

    [RelayCommand]
    private async Task PlayPause()
    {
        await playback.TogglePlayPauseAsync();
    }

    [RelayCommand]
    private async Task Next()
    {
        await playback.GoToNextAsync();
    }

    [RelayCommand]
    private async Task Previous()
    {
        await playback.GoToPreviousAsync();
    }

    private void Playback_StateChanged(object? sender, EventArgs e)
    {
        // Coordinator events arrive on a background thread
        Dispatcher.UIThread.Post(() =>
        {
            ReaperPosition = playback.Position.PositionString;
            CurrentSong = playback.CurrentIndex >= 0
                ? playback.ActiveItems[playback.CurrentIndex].Region!.Value.Name
                : "—";
        });
    }

    private void RegionCatalog_RegionsChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(UpdateRegionsText);
    }

    private void UpdateRegionsText()
    {
        Regions = string.Join("\n", regionCatalog.Regions.Select(t => $"{t.Name} ({t.StartPosition} - {t.EndPosition})"));
    }

    private KeyGesture ParseGesture(string gesture, Key fallback)
    {
        try
        {
            return KeyGesture.Parse(gesture);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Invalid key gesture '{Gesture}', falling back to {Fallback}", gesture, fallback);
            return new KeyGesture(fallback);
        }
    }
}
