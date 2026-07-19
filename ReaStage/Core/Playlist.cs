using System;
using System.Collections.Generic;

namespace ReaStage.Core;

public class Playlist
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public List<PlaylistItem> Items { get; set; } = [];
}
