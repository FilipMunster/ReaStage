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
        PlaylistEditorViewModel playlistEditor,
        ILogger<MainWindowViewModel> logger)
    {
        this.playback = playback;
        this.settingsService = settingsService;
        this.logger = logger;
        Stage = stage;
        Settings = settings;
        PlaylistEditor = playlistEditor;
        currentPage = stage;
        logger.LogDebug("MainWindowViewModel created");

        ReloadGestures();
        settingsService.SettingsSaved += (_, _) => ReloadGestures();
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
                LeaveCurrentPage();
                return true;
            }

            return Stage.ClearArm();
        }

        // Driven from the window: the key buttons are not focusable, so the event
        // would never tunnel down into the settings view on its own
        if (CurrentPage == Settings && Settings.IsCapturing)
        {
            if (!IsModifierKey(e.Key))
            {
                Settings.ApplyCapturedKey(new KeyGesture(e.Key, e.KeyModifiers));
            }

            return true;
        }

        // Transport keys work on the whole stage page, the open side pane included.
        // Letting them fall through there once opened Settings mid-concert, because
        // Space activated the focused menu button. The pane holds no text input, so
        // the keys close it and act instead.
        if (CurrentPage != Stage)
        {
            return false;
        }

        if (playPauseGesture.Matches(e))
        {
            IsPaneOpen = false;
            PlayPauseCommand.Execute(null);
            return true;
        }

        if (previousGesture.Matches(e))
        {
            IsPaneOpen = false;
            PreviousCommand.Execute(null);
            return true;
        }

        if (nextGesture.Matches(e))
        {
            IsPaneOpen = false;
            NextCommand.Execute(null);
            return true;
        }

        return false;
    }

    // On the stage the icon opens the menu; on the other pages it means "done" —
    // it applies the changes and goes back
    [RelayCommand]
    private void TogglePane()
    {
        if (CurrentPage != Stage)
        {
            LeaveCurrentPage();
            return;
        }

        IsPaneOpen = !IsPaneOpen;
    }

    // Returns false when the page refused to be left, which only happens when the
    // settings hold an invalid value; the error is already shown on the page
    private bool LeaveCurrentPage()
    {
        if (CurrentPage == Settings && !Settings.TrySave())
        {
            IsPaneOpen = false;
            return false;
        }

        ShowStage();
        return true;
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

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin;
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
