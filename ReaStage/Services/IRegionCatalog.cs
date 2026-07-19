using ReaStage.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReaStage.Services;

public interface IRegionCatalog
{
    // Raised on a background thread and only when the region list actually changed
    event EventHandler? RegionsChanged;

    // Raised on a background thread when IsReaperReachable flips
    event EventHandler? ReachabilityChanged;

    IReadOnlyList<ReaperRegion> Regions { get; }

    // Result of the last poll; false until the first successful refresh
    bool IsReaperReachable { get; }

    Task RefreshAsync();
}
