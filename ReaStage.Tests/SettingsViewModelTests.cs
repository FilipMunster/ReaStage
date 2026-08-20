using ReaStage.Core;
using ReaStage.Services;
using ReaStage.ViewModels;

namespace ReaStage.Tests;

public class SettingsViewModelTests
{
    private sealed class FakeSettingsService : ISettingsService
    {
        public event EventHandler? SettingsSaved;

        public AppSettings Settings { get; } = new();

        public int SaveCount { get; private set; }

        public void Save()
        {
            SaveCount++;
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
    }

    private readonly FakeSettingsService settingsService = new();

    private SettingsViewModel CreateLoadedViewModel()
    {
        SettingsViewModel viewModel = new SettingsViewModel(settingsService);
        viewModel.Load();
        return viewModel;
    }


    [Fact]
    public void Load_ShowsCurrentValues()
    {
        settingsService.Settings.PreviousThresholdSeconds = 5;
        settingsService.Settings.Keys.PlayPause = "P";

        SettingsViewModel viewModel = CreateLoadedViewModel();

        Assert.Equal("5", viewModel.PreviousThresholdSeconds);
        Assert.Equal("P", viewModel.PlayPauseKey);
    }

    [Fact]
    public void TrySave_ValidValues_WritesSettings()
    {
        SettingsViewModel viewModel = CreateLoadedViewModel();

        viewModel.PlayPauseKey = "P";
        viewModel.PreviousThresholdSeconds = "2,5";

        Assert.True(viewModel.TrySave());
        Assert.Equal(1, settingsService.SaveCount);
        Assert.Equal("P", settingsService.Settings.Keys.PlayPause);
        Assert.Equal(2.5, settingsService.Settings.PreviousThresholdSeconds);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
    }

    [Fact]
    public void TrySave_InvalidGesture_FailsWithErrorAndDoesNotSave()
    {
        SettingsViewModel viewModel = CreateLoadedViewModel();

        viewModel.PlayPauseKey = "NotAKey123";

        Assert.False(viewModel.TrySave());
        Assert.Equal(0, settingsService.SaveCount);
        Assert.Contains("NotAKey123", viewModel.ErrorMessage);
    }

    [Fact]
    public void TrySave_PortOutOfRange_FailsWithErrorAndDoesNotSave()
    {
        SettingsViewModel viewModel = CreateLoadedViewModel();

        viewModel.ReaperHttpPort = "99999";

        Assert.False(viewModel.TrySave());
        Assert.Equal(0, settingsService.SaveCount);
        Assert.Contains("99999", viewModel.ErrorMessage);
    }

    [Fact]
    public void RevertChanges_ThrowsAwayEditsAndSavesNothing()
    {
        SettingsViewModel viewModel = CreateLoadedViewModel();

        viewModel.PreviousThresholdSeconds = "9";
        viewModel.RevertChangesCommand.Execute(null);

        Assert.Equal("3", viewModel.PreviousThresholdSeconds);
        Assert.Equal(0, settingsService.SaveCount);
        Assert.Equal(3.0, settingsService.Settings.PreviousThresholdSeconds);
    }

    [Fact]
    public void RevertChanges_ClearsThePendingError()
    {
        SettingsViewModel viewModel = CreateLoadedViewModel();
        viewModel.ReaperHttpPort = "99999";
        viewModel.TrySave();

        viewModel.RevertChangesCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.ErrorMessage);
    }

    // Shown at the bottom of the settings page; the commit suffix is what tells a
    // stale build from a fresh one, so it must survive the formatting
    [Fact]
    public void VersionText_IsVersionWithShortCommit()
    {
        SettingsViewModel viewModel = CreateLoadedViewModel();

        Assert.Matches(@"^\d+\.\d+\.\d+ \([0-9a-f]{7}\)$", viewModel.VersionText);
    }
}
