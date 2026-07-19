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
    private readonly ISettingsService settingsService;

    public event EventHandler? Closed;

    [ObservableProperty]
    private string playPauseKey = string.Empty;

    [ObservableProperty]
    private string previousKey = string.Empty;

    [ObservableProperty]
    private string nextKey = string.Empty;

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

    // Called every time the settings page is shown
    public void Load()
    {
        AppSettings settings = settingsService.Settings;

        PlayPauseKey = settings.Keys.PlayPause;
        PreviousKey = settings.Keys.Previous;
        NextKey = settings.Keys.Next;
        PreviousSongsShown = settings.PreviousSongsShown.ToString(CultureInfo.InvariantCulture);
        NextSongsShown = settings.NextSongsShown.ToString(CultureInfo.InvariantCulture);
        PreviousThresholdSeconds = settings.PreviousThresholdSeconds.ToString(CultureInfo.InvariantCulture);
        RegionsPollSeconds = settings.RegionsPollSeconds.ToString(CultureInfo.InvariantCulture);
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
            || !TryParseInt(PreviousSongsShown, 0, 20, "Počet předchozích písní", out int previousShown)
            || !TryParseInt(NextSongsShown, 0, 20, "Počet následujících písní", out int nextShown)
            || !TryParseDouble(PreviousThresholdSeconds, 0, 60, "Práh šipky zpět", out double threshold)
            || !TryParseInt(RegionsPollSeconds, 1, 3600, "Interval aktualizace regionů", out int pollSeconds)
            || !TryParseHost(ReaperHost)
            || !TryParseInt(ReaperHttpPort, 1, 65535, "HTTP port", out int httpPort)
            || !TryParseInt(ReaperOscPort, 1, 65535, "OSC port", out int oscPort))
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
