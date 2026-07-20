using Microsoft.Extensions.Logging.Abstractions;
using ReaStage.Core;
using ReaStage.Services;
using ReaStage.ViewModels;

namespace ReaStage.Tests;

public class PlaylistEditorViewModelTests : IDisposable
{
    private sealed class FakeRegionCatalog : IRegionCatalog
    {
        public event EventHandler? RegionsChanged;

        public event EventHandler? ReachabilityChanged
        {
            add
            {
            }
            remove
            {
            }
        }

        public IReadOnlyList<ReaperRegion> Regions { get; set; } =
        [
            new ReaperRegion(1, "One", 0, 100, null),
            new ReaperRegion(2, "Two", 110, 230, null),
            new ReaperRegion(3, "Three", 240, 300, null)
        ];

        public bool IsReaperReachable => true;

        public Task RefreshAsync() => Task.CompletedTask;

        public void RaiseRegionsChanged()
        {
            RegionsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private readonly string databasePath;
    private readonly PlaylistService playlistService;
    private readonly FakeRegionCatalog catalog = new();

    public PlaylistEditorViewModelTests()
    {
        databasePath = Path.Combine(Path.GetTempPath(), "ReaStageTests", $"{Guid.NewGuid()}.json");
        playlistService = new PlaylistService(NullLogger<PlaylistService>.Instance, databasePath);
    }

    public void Dispose()
    {
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }

    private PlaylistEditorViewModel CreateLoadedViewModel()
    {
        PlaylistEditorViewModel viewModel = new PlaylistEditorViewModel(playlistService, catalog, action => action());
        viewModel.Load();
        return viewModel;
    }

    private static PlaylistEditorViewModel.SongItemViewModel Song(PlaylistEditorViewModel viewModel, int columnIndex, int songIndex)
    {
        return viewModel.Columns[columnIndex].Songs[songIndex];
    }

    [Fact]
    public void Load_FirstColumnIsReadOnlyReaperInTimelineOrder()
    {
        PlaylistEditorViewModel viewModel = CreateLoadedViewModel();

        PlaylistEditorViewModel.PlaylistColumnViewModel reaper = viewModel.Columns[0];
        Assert.True(reaper.IsReadOnly);
        Assert.Equal("REAPER", reaper.Name);
        Assert.Equal(["One", "Two", "Three"], reaper.Songs.Select(s => s.Name));
        Assert.All(reaper.Songs, s => Assert.False(s.CanDrag));
    }

    [Fact]
    public void CreatePlaylist_CopiesReaperRegionsWithUniqueNames()
    {
        PlaylistEditorViewModel viewModel = CreateLoadedViewModel();

        viewModel.CreatePlaylistCommand.Execute(null);
        viewModel.CreatePlaylistCommand.Execute(null);

        Assert.Equal(3, viewModel.Columns.Count);
        Assert.Equal("Nový playlist", viewModel.Columns[1].Name);
        Assert.Equal("Nový playlist (2)", viewModel.Columns[2].Name);
        Assert.Equal(["One", "Two", "Three"], viewModel.Columns[1].Songs.Select(s => s.Name));
    }

    [Fact]
    public void DeleteSong_MovesToDeletedSection()
    {
        PlaylistEditorViewModel viewModel = CreateLoadedViewModel();
        viewModel.CreatePlaylistCommand.Execute(null);

        viewModel.DeleteSongCommand.Execute(Song(viewModel, 1, 1));

        PlaylistEditorViewModel.PlaylistColumnViewModel column = viewModel.Columns[1];
        Assert.Equal(["One", "Three"], column.Songs.Select(s => s.Name));
        Assert.Equal(["Two"], column.DeletedSongs.Select(s => s.Name));
        Assert.True(column.HasDeletedSongs);
    }

    [Fact]
    public void RestoreSong_ReturnsToOriginalPosition()
    {
        PlaylistEditorViewModel viewModel = CreateLoadedViewModel();
        viewModel.CreatePlaylistCommand.Execute(null);
        viewModel.DeleteSongCommand.Execute(Song(viewModel, 1, 1));

        viewModel.RestoreSongCommand.Execute(viewModel.Columns[1].DeletedSongs[0]);

        PlaylistEditorViewModel.PlaylistColumnViewModel column = viewModel.Columns[1];
        Assert.Equal(["One", "Two", "Three"], column.Songs.Select(s => s.Name));
        Assert.False(column.HasDeletedSongs);
    }

    [Fact]
    public void PreviewMoveToIndex_ReordersVisualOnlyWithoutPersisting()
    {
        PlaylistEditorViewModel viewModel = CreateLoadedViewModel();
        viewModel.CreatePlaylistCommand.Execute(null);
        Guid id = viewModel.Columns[1].Id!.Value;

        viewModel.PreviewMoveToIndex(Song(viewModel, 1, 0), 2);

        Assert.Equal(["Two", "Three", "One"], viewModel.Columns[1].Songs.Select(s => s.Name));
        Assert.Equal([1, 2, 3], playlistService.GetPlaylist(id)!.Items.Select(i => i.RegionId));
    }

    [Fact]
    public void PreviewMoveToIndex_IsIdempotent()
    {
        PlaylistEditorViewModel viewModel = CreateLoadedViewModel();
        viewModel.CreatePlaylistCommand.Execute(null);
        PlaylistEditorViewModel.SongItemViewModel dragged = Song(viewModel, 1, 0);

        // Repeating the same target index (as a stationary cursor would) must not oscillate
        viewModel.PreviewMoveToIndex(dragged, 1);
        viewModel.PreviewMoveToIndex(dragged, 1);
        viewModel.PreviewMoveToIndex(dragged, 1);

        Assert.Equal(["Two", "One", "Three"], viewModel.Columns[1].Songs.Select(s => s.Name));
    }

    [Fact]
    public void PreviewMoveToIndex_ClampsOutOfRange()
    {
        PlaylistEditorViewModel viewModel = CreateLoadedViewModel();
        viewModel.CreatePlaylistCommand.Execute(null);

        viewModel.PreviewMoveToIndex(Song(viewModel, 1, 0), 99);

        Assert.Equal(["Two", "Three", "One"], viewModel.Columns[1].Songs.Select(s => s.Name));
    }

    [Fact]
    public void CommitDrag_Down_PersistsPreviewedOrder()
    {
        PlaylistEditorViewModel viewModel = CreateLoadedViewModel();
        viewModel.CreatePlaylistCommand.Execute(null);
        Guid id = viewModel.Columns[1].Id!.Value;
        PlaylistEditorViewModel.SongItemViewModel dragged = Song(viewModel, 1, 0);

        viewModel.PreviewMoveToIndex(dragged, 2);
        viewModel.CommitDrag(dragged);

        Assert.Equal([2, 3, 1], playlistService.GetPlaylist(id)!.Items.Select(i => i.RegionId));
        Assert.Equal(["Two", "Three", "One"], viewModel.Columns[1].Songs.Select(s => s.Name));
    }

    [Fact]
    public void CommitDrag_Up_PersistsPreviewedOrder()
    {
        PlaylistEditorViewModel viewModel = CreateLoadedViewModel();
        viewModel.CreatePlaylistCommand.Execute(null);
        Guid id = viewModel.Columns[1].Id!.Value;
        PlaylistEditorViewModel.SongItemViewModel dragged = Song(viewModel, 1, 2);

        viewModel.PreviewMoveToIndex(dragged, 0);
        viewModel.CommitDrag(dragged);

        Assert.Equal([3, 1, 2], playlistService.GetPlaylist(id)!.Items.Select(i => i.RegionId));
        Assert.Equal(["Three", "One", "Two"], viewModel.Columns[1].Songs.Select(s => s.Name));
    }

    [Fact]
    public void CancelDrag_RestoresPersistedOrder()
    {
        PlaylistEditorViewModel viewModel = CreateLoadedViewModel();
        viewModel.CreatePlaylistCommand.Execute(null);

        viewModel.PreviewMoveToIndex(Song(viewModel, 1, 0), 2);
        viewModel.CancelDrag();

        Assert.Equal(["One", "Two", "Three"], viewModel.Columns[1].Songs.Select(s => s.Name));
    }

    [Fact]
    public void PreviewMoveToIndex_OnlyAffectsSourceColumn()
    {
        PlaylistEditorViewModel viewModel = CreateLoadedViewModel();
        viewModel.CreatePlaylistCommand.Execute(null);
        viewModel.CreatePlaylistCommand.Execute(null);

        viewModel.PreviewMoveToIndex(Song(viewModel, 1, 0), 2);

        Assert.Equal(["Two", "Three", "One"], viewModel.Columns[1].Songs.Select(s => s.Name));
        Assert.Equal(["One", "Two", "Three"], viewModel.Columns[2].Songs.Select(s => s.Name));
    }

    [Fact]
    public void CommitDrag_WithDeletedItem_KeepsRawIndexMapping()
    {
        PlaylistEditorViewModel viewModel = CreateLoadedViewModel();
        viewModel.CreatePlaylistCommand.Execute(null);
        Guid id = viewModel.Columns[1].Id!.Value;
        viewModel.DeleteSongCommand.Execute(Song(viewModel, 1, 1)); // deletes "Two" (region 2)

        // Visual songs are now [One, Three]; drag "One" after "Three"
        PlaylistEditorViewModel.SongItemViewModel dragged = Song(viewModel, 1, 0);
        viewModel.PreviewMoveToIndex(dragged, 1);
        viewModel.CommitDrag(dragged);

        Assert.Equal(["Three", "One"], viewModel.Columns[1].Songs.Select(s => s.Name));
        Assert.Equal(["Two"], viewModel.Columns[1].DeletedSongs.Select(s => s.Name));
    }

    [Fact]
    public void MissingRegion_IsMarked()
    {
        PlaylistEditorViewModel viewModel = CreateLoadedViewModel();
        viewModel.CreatePlaylistCommand.Execute(null);
        catalog.Regions = [new ReaperRegion(1, "One", 0, 100, null), new ReaperRegion(3, "Three", 240, 300, null)];

        catalog.RaiseRegionsChanged();

        PlaylistEditorViewModel.SongItemViewModel missing = viewModel.Columns[1].Songs[1];
        Assert.True(missing.IsMissing);
        Assert.Contains("2", missing.Name);
    }

    [Fact]
    public void RenameColumn_UpdatesPlaylist()
    {
        PlaylistEditorViewModel viewModel = CreateLoadedViewModel();
        viewModel.CreatePlaylistCommand.Execute(null);
        Guid id = viewModel.Columns[1].Id!.Value;

        viewModel.Columns[1].Name = "Sobotní koncert";

        Assert.Equal("Sobotní koncert", playlistService.GetPlaylist(id)!.Name);
    }

    [Fact]
    public void RequestDeletePlaylist_SetsPendingWithoutDeleting()
    {
        PlaylistEditorViewModel viewModel = CreateLoadedViewModel();
        viewModel.CreatePlaylistCommand.Execute(null);
        Guid id = viewModel.Columns[1].Id!.Value;

        viewModel.RequestDeletePlaylistCommand.Execute(viewModel.Columns[1]);

        Assert.NotNull(viewModel.PendingDeleteColumn);
        Assert.NotNull(playlistService.GetPlaylist(id));
    }

    [Fact]
    public void ConfirmDeletePlaylist_RemovesColumn()
    {
        PlaylistEditorViewModel viewModel = CreateLoadedViewModel();
        viewModel.CreatePlaylistCommand.Execute(null);
        Guid id = viewModel.Columns[1].Id!.Value;

        viewModel.RequestDeletePlaylistCommand.Execute(viewModel.Columns[1]);
        viewModel.ConfirmDeletePlaylistCommand.Execute(null);

        Assert.Null(viewModel.PendingDeleteColumn);
        Assert.Null(playlistService.GetPlaylist(id));
        Assert.Single(viewModel.Columns); // only the REAPER column remains
    }

    [Fact]
    public void CancelDeletePlaylist_KeepsPlaylist()
    {
        PlaylistEditorViewModel viewModel = CreateLoadedViewModel();
        viewModel.CreatePlaylistCommand.Execute(null);
        Guid id = viewModel.Columns[1].Id!.Value;

        viewModel.RequestDeletePlaylistCommand.Execute(viewModel.Columns[1]);
        viewModel.CancelDeletePlaylistCommand.Execute(null);

        Assert.Null(viewModel.PendingDeleteColumn);
        Assert.NotNull(playlistService.GetPlaylist(id));
    }

    [Fact]
    public void DuplicatePlaylist_AddsColumn()
    {
        PlaylistEditorViewModel viewModel = CreateLoadedViewModel();
        viewModel.CreatePlaylistCommand.Execute(null);
        viewModel.DeleteSongCommand.Execute(Song(viewModel, 1, 0));

        viewModel.DuplicatePlaylistCommand.Execute(viewModel.Columns[1]);

        Assert.Equal(3, viewModel.Columns.Count);
        Assert.Equal("Nový playlist (kopie)", viewModel.Columns[2].Name);
        Assert.Equal(["Two", "Three"], viewModel.Columns[2].Songs.Select(s => s.Name));
        Assert.Equal(["One"], viewModel.Columns[2].DeletedSongs.Select(s => s.Name));
    }
}
