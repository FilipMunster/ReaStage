using ReaStage.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReaStage.Services;

public interface IRegionCatalog
{
    // Raised on a background thread and only when the region list actually changed
    event EventHandler? RegionsChanged;

    IReadOnlyList<ReaperRegion> Regions { get; }

    Task RefreshAsync();
}
