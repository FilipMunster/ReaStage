using Microsoft.Extensions.Logging;
using ReaStage.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ReaStage.Services;

internal class RegionCatalog : IRegionCatalog, IDisposable
{
    private readonly IReaperClient client;
    private readonly ISettingsService settingsService;
    private readonly ILogger<RegionCatalog> logger;
    private readonly CancellationTokenSource cts = new();

    public event EventHandler? RegionsChanged;

    public event EventHandler? ReachabilityChanged;

    public IReadOnlyList<ReaperRegion> Regions { get; private set; } = [];

    public bool IsReaperReachable { get; private set; }

    public RegionCatalog(
        IReaperClient client,
        ISettingsService settingsService,
        ILogger<RegionCatalog> logger)
    {
        this.client = client;
        this.settingsService = settingsService;
        this.logger = logger;

        _ = PollLoopAsync(cts.Token);
    }

    public async Task RefreshAsync()
    {
        try
        {
            List<ReaperRegion> regions = await client.GetRegions();
            SetReachable(true);
            if (!regions.SequenceEqual(Regions))
            {
                Regions = regions;
                logger.LogInformation("Regions updated, {Count} regions", regions.Count);
                RegionsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            // Keep the last known regions when REAPER is unreachable
            logger.LogError(ex, "Failed to refresh regions from REAPER");
            SetReachable(false);
        }
    }

    private void SetReachable(bool reachable)
    {
        if (IsReaperReachable != reachable)
        {
            IsReaperReachable = reachable;
            ReachabilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await RefreshAsync();

            int seconds = Math.Max(1, settingsService.Settings.RegionsPollSeconds);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        cts.Cancel();
        cts.Dispose();
    }
}
