using ReaStage.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReaStage.Services;

public interface IPlaybackCoordinator
{
    // Raised on a background thread — consumers must marshal to the UI thread
    event EventHandler? StateChanged;

    // Playable items of the active playlist (deleted and missing items excluded)
    IReadOnlyList<ResolvedPlaylistItem> ActiveItems { get; }

    // Index into ActiveItems, -1 = position is not inside any song
    int CurrentIndex { get; }

    ReaperPosition Position { get; }

    Task TogglePlayPauseAsync();
    Task GoToNextAsync();
    Task GoToPreviousAsync();
}
