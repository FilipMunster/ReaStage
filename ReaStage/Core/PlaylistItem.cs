namespace ReaStage.Core;

public class PlaylistItem
{
    public int RegionId { get; set; }

    // Soft delete: the item stays in the playlist but is skipped by playback
    // and shown in a separated section at the end of the editor column
    public bool Deleted { get; set; }
}
