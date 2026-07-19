using System.Collections.Generic;
using System.Linq;

namespace ReaStage.Core;

public static class PlaylistResolver
{
    public static List<ResolvedPlaylistItem> Resolve(Playlist playlist, IReadOnlyCollection<ReaperRegion> regions)
    {
        Dictionary<int, ReaperRegion> regionsById = regions.ToDictionary(r => r.Id);

        return playlist.Items
            .Select(item => new ResolvedPlaylistItem(
                item,
                regionsById.TryGetValue(item.RegionId, out ReaperRegion region) ? region : null))
            .ToList();
    }
}
