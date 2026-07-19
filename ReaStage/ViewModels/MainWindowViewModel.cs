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
    private readonly ILogger<MainWindowViewModel> logger;

    [ObservableProperty]
    private string reaperPosition = string.Empty;

    [ObservableProperty]
    private string regions = string.Empty;

    public MainWindowViewModel(
        IReaperClient client,
        ILogger<MainWindowViewModel> logger)
    {
        this.client = client;
        this.logger = logger;
        logger.LogDebug("MainWindowViewModel created");

        client.PositionChanged += Client_PositionChanged;

        Task.Run(async () =>
        {
            List<ReaperRegion> regions = await GetRegions();
            string regionsText = string.Join("\n", regions.Select(t => $"{t.Name} ({t.StartPosition} - {t.EndPosition})"));
            Dispatcher.UIThread.Post(() => Regions = regionsText);
        });
    }

    private void Client_PositionChanged(object? sender, ReaperPositionChangedEventArgs e)
    {
        // OSC events arrive on a background thread
        Dispatcher.UIThread.Post(() => ReaperPosition = e.Position.PositionString);
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


    private async Task<List<ReaperRegion>> GetRegions()
    {
        try
        {
            List<ReaperRegion> regions = await client.GetRegions();
            logger.LogInformation("Retrieved {Count} regions from Reaper", regions.Count);
            return regions;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve regions from Reaper");
            return [];
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
