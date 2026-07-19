using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using ReaStage.Core;
using ReaStage.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ReaStage.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IReaperClient client;
    private readonly IRegionCatalog regionCatalog;
    private readonly ILogger<MainWindowViewModel> logger;

    [ObservableProperty]
    private string reaperPosition = string.Empty;

    [ObservableProperty]
    private string regions = string.Empty;

    public MainWindowViewModel(
        IReaperClient client,
        IRegionCatalog regionCatalog,
        ILogger<MainWindowViewModel> logger)
    {
        this.client = client;
        this.regionCatalog = regionCatalog;
        this.logger = logger;
        logger.LogDebug("MainWindowViewModel created");

        client.PositionChanged += Client_PositionChanged;
        regionCatalog.RegionsChanged += RegionCatalog_RegionsChanged;
        UpdateRegionsText();
    }

    private void Client_PositionChanged(object? sender, ReaperPositionChangedEventArgs e)
    {
        // OSC events arrive on a background thread
        Dispatcher.UIThread.Post(() => ReaperPosition = e.Position.PositionString);
    }

    private void RegionCatalog_RegionsChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(UpdateRegionsText);
    }

    private void UpdateRegionsText()
    {
        Regions = string.Join("\n", regionCatalog.Regions.Select(t => $"{t.Name} ({t.StartPosition} - {t.EndPosition})"));
    }

    [RelayCommand]
    private async Task PlayPause()
    {
        try
        {
            await client.SendPlayPause();
            logger.LogInformation("Sent Play/Pause command to Reaper");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Play/Pause command to Reaper");
        }
    }

    private async Task Stop()
    {
        try
        {
            await client.SendStop();
            logger.LogInformation("Sent Stop command to Reaper");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Stop command to Reaper");
        }
    }

    private async Task Play()
    {
        try
        {
            await client.SendPlay();
            logger.LogInformation("Sent Play command to Reaper");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Play command to Reaper");
        }
    }


    private async Task GetPosition()
    {
        try
        {
            var position = await client.GetPosition();
            logger.LogInformation("Retrieved position from Reaper: {Position}", position);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve position from Reaper");
        }
    }
}
