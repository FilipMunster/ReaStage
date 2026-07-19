namespace ReaStage.Core;

// Region is null when the referenced region no longer exists in REAPER
public readonly record struct ResolvedPlaylistItem(PlaylistItem Item, ReaperRegion? Region)
{
    public bool IsMissing => Region is null;
}
