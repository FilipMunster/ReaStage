using Avalonia.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ReaStage.Core;
using ReaStage.Services;
using System;
using System.Threading.Tasks;

namespace ReaStage.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IPlaybackCoordinator playback;
    private readonly ILogger<MainWindowViewModel> logger;
    private readonly KeyGesture playPauseGesture;
    private readonly KeyGesture previousGesture;
    private readonly KeyGesture nextGesture;

    public StageViewModel Stage { get; }

    public MainWindowViewModel(
        IPlaybackCoordinator playback,
        ISettingsService settingsService,
        StageViewModel stage,
        ILogger<MainWindowViewModel> logger)
    {
        this.playback = playback;
        this.logger = logger;
        Stage = stage;
        logger.LogDebug("MainWindowViewModel created");

        AppSettings.KeySettings keys = settingsService.Settings.Keys;
        playPauseGesture = ParseGesture(keys.PlayPause, Key.Space);
        previousGesture = ParseGesture(keys.Previous, Key.Left);
        nextGesture = ParseGesture(keys.Next, Key.Right);
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
