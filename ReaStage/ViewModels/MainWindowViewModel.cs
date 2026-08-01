using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
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
    private readonly ISettingsService settingsService;
    private readonly ILogger<MainWindowViewModel> logger;
    private KeyGesture playPauseGesture = null!;
    private KeyGesture previousGesture = null!;
    private KeyGesture nextGesture = null!;

    public StageViewModel Stage { get; }
    public SettingsViewModel Settings { get; }
    public PlaylistSelectionViewModel PlaylistSelection { get; }
    public PlaylistEditorViewModel PlaylistEditor { get; }

    [ObservableProperty]
    private ViewModelBase currentPage;

    [ObservableProperty]
    private bool isPaneOpen;

    public MainWindowViewModel(
        IPlaybackCoordinator playback,
        ISettingsService settingsService,
        StageViewModel stage,
        SettingsViewModel settings,
        PlaylistSelectionViewModel playlistSelection,
        PlaylistEditorViewModel playlistEditor,
        ILogger<MainWindowViewModel> logger)
    {
        this.playback = playback;
        this.settingsService = settingsService;
        this.logger = logger;
        Stage = stage;
        Settings = settings;
        PlaylistSelection = playlistSelection;
        PlaylistEditor = playlistEditor;
        currentPage = stage;
        logger.LogDebug("MainWindowViewModel created");

        ReloadGestures();
        settingsService.SettingsSaved += (_, _) => ReloadGestures();

        Settings.Closed += (_, _) => ShowStage();
        PlaylistSelection.Closed += (_, _) => ShowStage();
        PlaylistEditor.Closed += (_, _) => ShowStage();
    }

    // Returns true when the key was handled
    public bool HandleKey(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (IsPaneOpen)
            {
                IsPaneOpen = false;
                return true;
            }

            if (CurrentPage == Settings && Settings.CancelCaptureIfActive())
            {
                return true;
            }

            if (CurrentPage != Stage)
            {
                ShowStage();
                return true;
            }

            return Stage.ClearArm();
        }

        // Transport keys work only on the stage page with the pane closed
        if (CurrentPage != Stage || IsPaneOpen)
        {
            return false;
        }

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
    private void TogglePane()
    {
        IsPaneOpen = !IsPaneOpen;
    }

    [RelayCommand]
    private void ShowStage()
    {
        CurrentPage = Stage;
        IsPaneOpen = false;
    }

    [RelayCommand]
    private void ShowSettings()
    {
        Settings.Load();
        CurrentPage = Settings;
        IsPaneOpen = false;
    }

    [RelayCommand]
    private void ShowPlaylistSelection()
    {
        PlaylistSelection.Load();
        CurrentPage = PlaylistSelection;
        IsPaneOpen = false;
    }

    [RelayCommand]
    private void ShowPlaylistEditor()
    {
        PlaylistEditor.Load();
        CurrentPage = PlaylistEditor;
        IsPaneOpen = false;
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

    private void ReloadGestures()
    {
        AppSettings.KeySettings keys = settingsService.Settings.Keys;
        playPauseGesture = ParseGesture(keys.PlayPause, Key.Space);
        previousGesture = ParseGesture(keys.Previous, Key.Left);
        nextGesture = ParseGesture(keys.Next, Key.Right);
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
