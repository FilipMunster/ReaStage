using System;
using System.Collections.Generic;
using System.Globalization;

namespace ReaStage.Core;

// Pure helpers for the stage view statistics and time displays
public static class StageStats
{
    // "m:ss", or "h:mm:ss" from an hour up
    public static string FormatClock(double seconds)
    {
        if (seconds < 0)
        {
            seconds = 0;
        }

        int total = (int)Math.Floor(seconds);
        int hours = total / 3600;
        int minutes = total % 3600 / 60;
        int secs = total % 60;

        return hours > 0
            ? $"{hours}:{minutes:00}:{secs:00}"
            : $"{minutes}:{secs:00}";
    }

    // Position inside a song as "bar.beat", 1-based like REAPER's own readout
    public static string? FormatBarBeatPosition(double seconds, double tempo, int tsNumerator, int tsDenominator)
    {
        return FormatBarBeat(seconds, tempo, tsNumerator, tsDenominator, firstIndex: 1);
    }

    // Length of a stretch of music: whole bars and beats counted from zero, so two
    // bars left reads "2.0" and not "3.1"
    public static string? FormatBarBeatDuration(double seconds, double tempo, int tsNumerator, int tsDenominator)
    {
        return FormatBarBeat(seconds, tempo, tsNumerator, tsDenominator, firstIndex: 0);
    }

    // Assumes constant tempo; null when the tempo is not known yet
    private static string? FormatBarBeat(double seconds, double tempo, int tsNumerator, int tsDenominator, int firstIndex)
    {
        if (tempo <= 0 || tsNumerator <= 0 || tsDenominator <= 0)
        {
            return null;
        }

        if (seconds < 0)
        {
            seconds = 0;
        }

        double beatSeconds = 4.0 / tsDenominator * (60.0 / tempo);
        double totalBeats = seconds / beatSeconds;
        int bar = (int)(totalBeats / tsNumerator);
        int beat = (int)(totalBeats - bar * tsNumerator);

        return $"{bar + firstIndex}.{beat + firstIndex}";
    }

    // "4/4", empty when REAPER has not reported a time signature yet
    public static string FormatTimeSignature(int numerator, int denominator)
    {
        return numerator > 0 && denominator > 0
            ? $"{numerator}/{denominator}"
            : string.Empty;
    }

    // Beat within the current bar (1-based) out of "measures.beats.hundredths";
    // null when the string cannot be read
    public static int? BeatInBar(string positionStringBeats)
    {
        if (string.IsNullOrEmpty(positionStringBeats))
        {
            return null;
        }

        string[] parts = positionStringBeats.Split('.');
        if (parts.Length < 2
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int beat)
            || beat < 1)
        {
            return null;
        }

        return beat;
    }

    public static string FormatBpm(double tempo)
    {
        return tempo > 0
            ? $"{tempo.ToString("0.#", CultureInfo.InvariantCulture)} BPM"
            : "— BPM";
    }

    // "N / M", or "– / M" when no song is current
    public static string OrderText(int index, int count)
    {
        return index >= 0
            ? $"{index + 1} / {count}"
            : $"– / {count}";
    }

    // Pure musical time to the end of the playlist: rest of the current song plus
    // the full lengths of the songs that follow (gaps between songs not predicted)
    public static double RemainingPlaylistSeconds(IReadOnlyList<ResolvedPlaylistItem> items, int index, double positionSeconds)
    {
        double remaining = 0;

        if (index >= 0)
        {
            ReaperRegion current = items[index].Region!.Value;
            remaining += Math.Max(0, current.EndPosition - positionSeconds);

            for (int i = index + 1; i < items.Count; i++)
            {
                ReaperRegion region = items[i].Region!.Value;
                remaining += region.EndPosition - region.StartPosition;
            }
        }
        else
        {
            // Between songs or before the start: count the songs still ahead in time
            foreach (ResolvedPlaylistItem item in items)
            {
                ReaperRegion region = item.Region!.Value;
                if (region.StartPosition >= positionSeconds)
                {
                    remaining += region.EndPosition - region.StartPosition;
                }
            }
        }

        return remaining;
    }

    // Wall-clock estimate of when the playlist ends, "HH:mm"
    public static string FormatEndEstimate(DateTime now, double remainingSeconds)
    {
        return now.AddSeconds(remainingSeconds).ToString("HH:mm", CultureInfo.InvariantCulture);
    }
}
