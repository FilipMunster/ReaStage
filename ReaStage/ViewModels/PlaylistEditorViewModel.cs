using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReaStage.Core;
using ReaStage.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ReaStage.ViewModels;

public partial class PlaylistEditorViewModel : ViewModelBase
{
    public class SongItemViewModel
    {
        // null = the read-only REAPER column
        public Guid? PlaylistId { get; init; }

        // Raw index into Playlist.Items, -1 for the REAPER column
        public int ItemIndex { get; init; } = -1;

        public string Name { get; init; } = string.Empty;
        public string LengthText { get; init; } = string.Empty;
        public bool IsMissing { get; init; }
        public bool IsDeleted { get; init; }

        public bool CanEdit => PlaylistId is not null;
        public bool CanDrag => PlaylistId is not null && !IsDeleted;
    }

    public partial class PlaylistColumnViewModel : ViewModelBase
    {
        private readonly Action<Guid, string>? renameCallback;

        [ObservableProperty]
        private string name = string.Empty;

        public Guid? Id { get; }
        public bool IsReadOnly => Id is null;
        public IReadOnlyList<SongItemViewModel> Songs { get; }
        public IReadOnlyList<SongItemViewModel> DeletedSongs { get; }
        public bool HasDeletedSongs => DeletedSongs.Count > 0;

        public PlaylistColumnViewModel(
            Guid? id,
            string name,
            IReadOnlyList<SongItemViewModel> songs,
            IReadOnlyList<SongItemViewModel> deletedSongs,
            Action<Guid, string>? renameCallback)
        {
            Id = id;
            this.name = name;
            Songs = songs;
            DeletedSongs = deletedSongs;
            this.renameCallback = renameCallback;
        }

        partial void OnNameChanged(string value)
        {
            if (Id is Guid id && !string.IsNullOrWhiteSpace(value))
            {
                renameCallback?.Invoke(id, value);
            }
        }
    }

    private readonly IPlaylistService playlistService;
    private readonly IRegionCatalog regionCatalog;
    private readonly Action<Action> dispatch;

    public event EventHandler? Closed;

    [ObservableProperty]
    private IReadOnlyList<PlaylistColumnViewModel> columns = [];

    public PlaylistEditorViewModel(IPlaylistService playlistService, IRegionCatalog regionCatalog)
        : this(playlistService, regionCatalog, action => Dispatcher.UIThread.Post(action))
    {
    }

    // Test constructor with a synchronous dispatcher
    internal PlaylistEditorViewModel(
        IPlaylistService playlistService,
        IRegionCatalog regionCatalog,
        Action<Action> dispatch)
    {
        this.playlistService = playlistService;
        this.regionCatalog = regionCatalog;
        this.dispatch = dispatch;

        playlistService.PlaylistsChanged += (_, _) => this.dispatch(Rebuild);
        regionCatalog.RegionsChanged += (_, _) => this.dispatch(Rebuild);
    }

    // Called every time the editor page is shown
    public void Load()
    {
        Rebuild();
        _ = regionCatalog.RefreshAsync();
    }

    // Drop on a song: the dragged song is inserted at the target's position
    public void MoveSong(SongItemViewModel source, SongItemViewModel target)
    {
        if (source.PlaylistId is not Guid playlistId
            || target.PlaylistId != source.PlaylistId
            || target.IsDeleted
            || source.ItemIndex == target.ItemIndex)
        {
            return;
        }

        int toIndex = source.ItemIndex < target.ItemIndex ? target.ItemIndex - 1 : target.ItemIndex;
        playlistService.MoveItem(playlistId, source.ItemIndex, toIndex);
        Rebuild();
    }

    // Drop on the column's empty area: move to the end of the playlist
    public void MoveSongToEnd(SongItemViewModel source, PlaylistColumnViewModel column)
    {
        if (source.PlaylistId is not Guid playlistId || column.Id != playlistId)
        {
            return;
        }

        Playlist? playlist = playlistService.GetPlaylist(playlistId);
        if (playlist is null || source.ItemIndex == playlist.Items.Count - 1)
        {
            return;
        }

        playlistService.MoveItem(playlistId, source.ItemIndex, playlist.Items.Count - 1);
        Rebuild();
    }

    [RelayCommand]
    private void Back()
    {
        Closed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void CreatePlaylist()
    {
        string baseName = "Nový playlist";
        string name = baseName;
        int suffix = 2;
        while (playlistService.Playlists.Any(p => p.Name == name))
        {
            name = $"{baseName} ({suffix++})";
        }

        IEnumerable<int> regionIds = regionCatalog.Regions.OrderBy(r => r.StartPosition).Select(r => r.Id);
        playlistService.CreatePlaylist(name, regionIds);
        Rebuild();
    }

    [RelayCommand]
    private void DuplicatePlaylist(PlaylistColumnViewModel column)
    {
        if (column.Id is Guid id)
        {
            playlistService.DuplicatePlaylist(id);
            Rebuild();
        }
    }

    [RelayCommand]
    private void DeleteSong(SongItemViewModel song)
    {
        if (song.PlaylistId is Guid playlistId)
        {
            playlistService.SetItemDeleted(playlistId, song.ItemIndex, true);
            Rebuild();
        }
    }

    [RelayCommand]
    private void RestoreSong(SongItemViewModel song)
    {
        if (song.PlaylistId is Guid playlistId)
        {
            playlistService.SetItemDeleted(playlistId, song.ItemIndex, false);
            Rebuild();
        }
    }

    [RelayCommand]
    private async Task RefreshRegions()
    {
        await regionCatalog.RefreshAsync();
        Rebuild();
    }

    private void Rebuild()
    {
        IReadOnlyList<ReaperRegion> regions = regionCatalog.Regions;

        List<PlaylistColumnViewModel> result = [BuildReaperColumn(regions)];
        result.AddRange(playlistService.Playlists.Select(p => BuildPlaylistColumn(p, regions)));
        Columns = result;
    }

    private static PlaylistColumnViewModel BuildReaperColumn(IReadOnlyList<ReaperRegion> regions)
    {
        List<SongItemViewModel> songs = regions
            .OrderBy(r => r.StartPosition)
            .Select(r => new SongItemViewModel
            {
                Name = r.Name,
                LengthText = FormatLength(r)
            })
            .ToList();

        return new PlaylistColumnViewModel(null, "REAPER", songs, [], null);
    }

    private PlaylistColumnViewModel BuildPlaylistColumn(Playlist playlist, IReadOnlyList<ReaperRegion> regions)
    {
        List<ResolvedPlaylistItem> resolved = PlaylistResolver.Resolve(playlist, regions);

        List<SongItemViewModel> songs = [];
        List<SongItemViewModel> deletedSongs = [];
        for (int i = 0; i < resolved.Count; i++)
        {
            ResolvedPlaylistItem item = resolved[i];
            SongItemViewModel song = new SongItemViewModel
            {
                PlaylistId = playlist.Id,
                ItemIndex = i,
                Name = item.IsMissing ? $"Chybějící region {item.Item.RegionId}" : item.Region!.Value.Name,
                LengthText = item.IsMissing ? string.Empty : FormatLength(item.Region!.Value),
                IsMissing = item.IsMissing,
                IsDeleted = item.Item.Deleted
            };

            if (item.Item.Deleted)
            {
                deletedSongs.Add(song);
            }
            else
            {
                songs.Add(song);
            }
        }

        return new PlaylistColumnViewModel(playlist.Id, playlist.Name, songs, deletedSongs, RenamePlaylist);
    }

    private void RenamePlaylist(Guid id, string name)
    {
        if (playlistService.GetPlaylist(id)?.Name != name)
        {
            playlistService.RenamePlaylist(id, name);
        }
    }

    private static string FormatLength(ReaperRegion region)
    {
        TimeSpan length = TimeSpan.FromSeconds(Math.Max(0, region.EndPosition - region.StartPosition));
        return $"{(int)length.TotalMinutes}:{length.Seconds:00}";
    }
}
