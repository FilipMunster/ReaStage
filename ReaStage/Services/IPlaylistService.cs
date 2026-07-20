using ReaStage.Core;
using System;
using System.Collections.Generic;

namespace ReaStage.Services;

public interface IPlaylistService
{
    event EventHandler? PlaylistsChanged;

    IReadOnlyList<Playlist> Playlists { get; }

    // null = the virtual REAPER playlist (regions in timeline order) is active
    Guid? ActivePlaylistId { get; set; }

    Playlist? GetPlaylist(Guid id);
    Playlist CreatePlaylist(string name, IEnumerable<int> regionIds);
    Playlist DuplicatePlaylist(Guid id);
    void DeletePlaylist(Guid id);
    void RenamePlaylist(Guid id, string name);
    void MoveItem(Guid playlistId, int fromIndex, int toIndex);
    void SetItemDeleted(Guid playlistId, int itemIndex, bool deleted);
}
