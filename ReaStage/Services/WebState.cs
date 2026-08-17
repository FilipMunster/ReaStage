using ReaStage.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ReaStage.Services;

public sealed record WebSong(int Index, string Name);

// Snapshot of the stage state sent to the web client as JSON
public sealed record WebState(
    IReadOnlyList<WebSong> Songs,
    int CurrentIndex,
    double Progress,
    bool IsPlaying,
    bool ReaperReachable,
    string Bpm,
    string Order,
    string Elapsed,
    string Remaining,
    string RemainingPlaylist,
    string EndEstimate);

public static class WebStateBuilder
{
    public static WebState Build(
        IReadOnlyList<ResolvedPlaylistItem> items,
        int index,
        ReaperPosition position,
        bool reaperReachable,
        DateTime now)
    {
        List<WebSong> songs = items
            .Select((item, i) => new WebSong(i, item.Region!.Value.Name))
            .ToList();

        double progress = 0;
        string elapsed = string.Empty;
        string remaining = string.Empty;

        if (index >= 0)
        {
            ReaperRegion current = items[index].Region!.Value;
            double length = current.EndPosition - current.StartPosition;
            progress = length > 0
                ? Math.Clamp((position.PositionSeconds - current.StartPosition) / length, 0, 1)
                : 0;

            double elapsedSeconds = Math.Max(0, position.PositionSeconds - current.StartPosition);
            double remainingSeconds = Math.Max(0, current.EndPosition - position.PositionSeconds);

            elapsed = StageStats.FormatClock(elapsedSeconds);
            remaining = "−" + StageStats.FormatClock(remainingSeconds);
        }

        double remainingPlaylistSeconds = StageStats.RemainingPlaylistSeconds(items, index, position.PositionSeconds);

        return new WebState(
            Songs: songs,
            CurrentIndex: index,
            Progress: progress,
            IsPlaying: position.PlayState is ReaperPlayState.Playing or ReaperPlayState.Recording,
            ReaperReachable: reaperReachable,
            Bpm: StageStats.FormatBpm(position.TempoBpm),
            Order: StageStats.OrderText(index, items.Count),
            Elapsed: elapsed,
            Remaining: remaining,
            RemainingPlaylist: StageStats.FormatClock(remainingPlaylistSeconds),
            EndEstimate: StageStats.FormatEndEstimate(now, remainingPlaylistSeconds));
    }
}
