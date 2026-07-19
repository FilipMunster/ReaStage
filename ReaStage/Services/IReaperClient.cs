using ReaStage.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReaStage.Services;

public interface IReaperClient
{
    event EventHandler<ReaperPositionChangedEventArgs>? PositionChanged;

    Task<ReaperPosition> GetPosition();
    Task SetPosition(double seconds);
    Task<List<ReaperRegion>> GetRegions();
    Task SendPlay();
    Task SendPlayPause();
    Task SendStop();
}