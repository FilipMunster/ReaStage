using System;
using System.Collections.Generic;

namespace ReaStage.Core;

public class PlaylistDatabase
{
    public int Version { get; set; } = 1;

    // null = the virtual REAPER playlist (regions in timeline order) is active
    public Guid? ActivePlaylistId { get; set; }

    public List<Playlist> Playlists { get; set; } = [];
}
