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
    public void Save_ValidValues_WritesSettingsAndCloses()
    {
        SettingsViewModel viewModel = CreateLoadedViewModel();
        bool closed = false;
        viewModel.Closed += (_, _) => closed = true;

        viewModel.PlayPauseKey = "P";
        viewModel.PreviousThresholdSeconds = "2,5";
        viewModel.SaveCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal(1, settingsService.SaveCount);
        Assert.Equal("P", settingsService.Settings.Keys.PlayPause);
        Assert.Equal(2.5, settingsService.Settings.PreviousThresholdSeconds);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
    }

    [Fact]
    public void Save_InvalidGesture_ShowsErrorAndDoesNotSave()
    {
        SettingsViewModel viewModel = CreateLoadedViewModel();
        bool closed = false;
        viewModel.Closed += (_, _) => closed = true;

        viewModel.PlayPauseKey = "NotAKey123";
        viewModel.SaveCommand.Execute(null);

        Assert.False(closed);
        Assert.Equal(0, settingsService.SaveCount);
        Assert.Contains("NotAKey123", viewModel.ErrorMessage);
    }

    [Fact]
    public void Save_PortOutOfRange_ShowsErrorAndDoesNotSave()
    {
        SettingsViewModel viewModel = CreateLoadedViewModel();

        viewModel.ReaperHttpPort = "99999";
        viewModel.SaveCommand.Execute(null);

        Assert.Equal(0, settingsService.SaveCount);
        Assert.Contains("99999", viewModel.ErrorMessage);
    }

    [Fact]
    public void Back_ClosesWithoutSaving()
    {
        SettingsViewModel viewModel = CreateLoadedViewModel();
        bool closed = false;
        viewModel.Closed += (_, _) => closed = true;

        viewModel.PreviousThresholdSeconds = "9";
        viewModel.BackCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal(0, settingsService.SaveCount);
        Assert.Equal(3.0, settingsService.Settings.PreviousThresholdSeconds);
    }
}
