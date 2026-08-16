using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReaStage.Core;
using ReaStage.Services;
using System;
using System.Globalization;

namespace ReaStage.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private const string FieldPlayPause = "PlayPause";
    private const string FieldPrevious = "Previous";
    private const string FieldNext = "Next";
    private const string CapturePrompt = "Stiskni klávesu…";

    private readonly ISettingsService settingsService;

    public event EventHandler? Closed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseKeyDisplay))]
    private string playPauseKey = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviousKeyDisplay))]
    private string previousKey = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NextKeyDisplay))]
    private string nextKey = string.Empty;

    // Which key field is currently in "press a key" capture mode ("" = none)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCapturing))]
    [NotifyPropertyChangedFor(nameof(PlayPauseKeyDisplay))]
    [NotifyPropertyChangedFor(nameof(PreviousKeyDisplay))]
    [NotifyPropertyChangedFor(nameof(NextKeyDisplay))]
    private string capturingField = string.Empty;

    [ObservableProperty]
    private double fontScalePercent = 100;

    [ObservableProperty]
    private bool showMetronome = true;

    [ObservableProperty]
    private bool webEnabled;

    [ObservableProperty]
    private string webPort = string.Empty;

    [ObservableProperty]
    private string previousSongsShown = string.Empty;

    [ObservableProperty]
    private string nextSongsShown = string.Empty;

    [ObservableProperty]
    private string previousThresholdSeconds = string.Empty;

    [ObservableProperty]
    private string regionsPollSeconds = string.Empty;

    [ObservableProperty]
    private string reaperHost = string.Empty;

    [ObservableProperty]
    private string reaperHttpPort = string.Empty;

    [ObservableProperty]
    private string reaperOscPort = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public SettingsViewModel(ISettingsService settingsService)
    {
        this.settingsService = settingsService;
    }

    public bool IsCapturing => CapturingField.Length > 0;

    public string PlayPauseKeyDisplay => CapturingField == FieldPlayPause ? CapturePrompt : PlayPauseKey;

    public string PreviousKeyDisplay => CapturingField == FieldPrevious ? CapturePrompt : PreviousKey;

    public string NextKeyDisplay => CapturingField == FieldNext ? CapturePrompt : NextKey;

    [RelayCommand]
    private void StartCapture(string field)
    {
        CapturingField = field;
        ErrorMessage = string.Empty;
    }

    // Called by the view on Escape; returns true if a capture was in progress
    public bool CancelCaptureIfActive()
    {
        if (!IsCapturing)
        {
            return false;
        }

        CapturingField = string.Empty;
        return true;
    }

    // Called by the view when a key is pressed during capture
    public void ApplyCapturedKey(KeyGesture gesture)
    {
        if (!IsCapturing)
        {
            return;
        }

        string field = CapturingField;
        string text = gesture.ToString();

        // Reject assigning the same key to two actions
        bool duplicate = (field != FieldPlayPause && PlayPauseKey == text)
            || (field != FieldPrevious && PreviousKey == text)
            || (field != FieldNext && NextKey == text);
        if (duplicate)
        {
            ErrorMessage = $"Klávesa '{text}' je už přiřazena jiné akci.";
            CapturingField = string.Empty;
            return;
        }

        switch (field)
        {
            case FieldPlayPause:
                PlayPauseKey = text;
                break;
            case FieldPrevious:
                PreviousKey = text;
                break;
            case FieldNext:
                NextKey = text;
                break;
        }

        ErrorMessage = string.Empty;
        CapturingField = string.Empty;
    }

    // Called every time the settings page is shown
    public void Load()
    {
        AppSettings settings = settingsService.Settings;

        CapturingField = string.Empty;
        PlayPauseKey = settings.Keys.PlayPause;
        PreviousKey = settings.Keys.Previous;
        NextKey = settings.Keys.Next;
        PreviousSongsShown = settings.PreviousSongsShown.ToString(CultureInfo.InvariantCulture);
        NextSongsShown = settings.NextSongsShown.ToString(CultureInfo.InvariantCulture);
        PreviousThresholdSeconds = settings.PreviousThresholdSeconds.ToString(CultureInfo.InvariantCulture);
        RegionsPollSeconds = settings.RegionsPollSeconds.ToString(CultureInfo.InvariantCulture);
        FontScalePercent = settings.FontScalePercent;
        ShowMetronome = settings.ShowMetronome;
        WebEnabled = settings.Web.Enabled;
        WebPort = settings.Web.Port.ToString(CultureInfo.InvariantCulture);
        ReaperHost = settings.Reaper.Host;
        ReaperHttpPort = settings.Reaper.HttpPort.ToString(CultureInfo.InvariantCulture);
        ReaperOscPort = settings.Reaper.OscPort.ToString(CultureInfo.InvariantCulture);
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void Save()
    {
        if (!TryParseGesture(PlayPauseKey, "Play/Pauza")
            || !TryParseGesture(PreviousKey, "Předchozí")
            || !TryParseGesture(NextKey, "Další")
            || !ValidateNoDuplicateKeys()
            || !TryParseInt(PreviousSongsShown, 0, 20, "Počet předchozích písní", out int previousShown)
            || !TryParseInt(NextSongsShown, 0, 20, "Počet následujících písní", out int nextShown)
            || !TryParseDouble(PreviousThresholdSeconds, 0, 60, "Práh šipky zpět", out double threshold)
            || !TryParseInt(RegionsPollSeconds, 1, 3600, "Interval aktualizace regionů", out int pollSeconds)
            || !TryParseHost(ReaperHost)
            || !TryParseInt(ReaperHttpPort, 1, 65535, "HTTP port", out int httpPort)
            || !TryParseInt(ReaperOscPort, 1, 65535, "OSC port", out int oscPort)
            || !TryParseInt(WebPort, 1, 65535, "Port webu", out int webPort))
        {
            return;
        }

        AppSettings settings = settingsService.Settings;
        settings.Keys.PlayPause = PlayPauseKey.Trim();
        settings.Keys.Previous = PreviousKey.Trim();
        settings.Keys.Next = NextKey.Trim();
        settings.PreviousSongsShown = previousShown;
        settings.NextSongsShown = nextShown;
        settings.PreviousThresholdSeconds = threshold;
        settings.RegionsPollSeconds = pollSeconds;
        settings.FontScalePercent = (int)Math.Round(Math.Clamp(FontScalePercent, 50, 200));
        settings.ShowMetronome = ShowMetronome;
        settings.Web.Enabled = WebEnabled;
        settings.Web.Port = webPort;
        settings.Reaper.Host = ReaperHost.Trim();
        settings.Reaper.HttpPort = httpPort;
        settings.Reaper.OscPort = oscPort;

        settingsService.Save();
        ErrorMessage = string.Empty;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Back()
    {
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private bool ValidateNoDuplicateKeys()
    {
        string playPause = PlayPauseKey.Trim();
        string previous = PreviousKey.Trim();
        string next = NextKey.Trim();

        if (playPause == previous || playPause == next || previous == next)
        {
            ErrorMessage = "Každá akce musí mít jinou klávesu.";
            return false;
        }

        return true;
    }

    private bool TryParseGesture(string value, string fieldName)
    {
        try
        {
            KeyGesture.Parse(value.Trim());
            return true;
        }
        catch (Exception)
        {
            ErrorMessage = $"Neplatná klávesa pro '{fieldName}': {value}";
            return false;
        }
    }

    private bool TryParseInt(string value, int min, int max, string fieldName, out int result)
    {
        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result)
            && result >= min && result <= max)
        {
            return true;
        }

        ErrorMessage = $"Neplatná hodnota pro '{fieldName}': {value} (rozsah {min}–{max})";
        return false;
    }

    private bool TryParseDouble(string value, double min, double max, string fieldName, out double result)
    {
        string normalized = value.Trim().Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result)
            && result >= min && result <= max)
        {
            return true;
        }

        ErrorMessage = $"Neplatná hodnota pro '{fieldName}': {value} (rozsah {min}–{max})";
        return false;
    }

    private bool TryParseHost(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        ErrorMessage = "Host nesmí být prázdný";
        return false;
    }
}
