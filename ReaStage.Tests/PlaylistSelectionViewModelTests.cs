using Microsoft.Extensions.Logging.Abstractions;
using ReaStage.Core;
using ReaStage.Services;
using ReaStage.ViewModels;

namespace ReaStage.Tests;

public class PlaylistSelectionViewModelTests : IDisposable
{
    private sealed class FakeRegionCatalog : IRegionCatalog
    {
        public event EventHandler? RegionsChanged
        {
            add
            {
            }
            remove
            {
            }
        }

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
            new ReaperRegion(2, "Two", 110, 200, null)
        ];

        public bool IsReaperReachable => true;

        public Task RefreshAsync() => Task.CompletedTask;
    }

    private readonly string databasePath;
    private readonly PlaylistService playlistService;
    private readonly FakeRegionCatalog catalog = new();

    public PlaylistSelectionViewModelTests()
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

    private PlaylistSelectionViewModel CreateLoadedViewModel()
    {
        PlaylistSelectionViewModel viewModel = new PlaylistSelectionViewModel(playlistService, catalog);
        viewModel.Load();
        return viewModel;
    }

    [Fact]
    public void Load_FirstChoiceIsReaperPlaylist()
    {
        Playlist stored = playlistService.CreatePlaylist("Custom", [1, 2]);

        PlaylistSelectionViewModel viewModel = CreateLoadedViewModel();

        Assert.Equal(2, viewModel.Choices.Count);
        Assert.Null(viewModel.Choices[0].Id);
        Assert.True(viewModel.Choices[0].IsActive);
        Assert.Equal(2, viewModel.Choices[0].SongCount);
        Assert.Equal(stored.Id, viewModel.Choices[1].Id);
        Assert.False(viewModel.Choices[1].IsActive);
    }

    [Fact]
    public void Select_InactiveChoice_RequiresConfirmation()
    {
        Playlist stored = playlistService.CreatePlaylist("Custom", [1]);
        PlaylistSelectionViewModel viewModel = CreateLoadedViewModel();

        viewModel.SelectCommand.Execute(viewModel.Choices[1]);

        Assert.NotNull(viewModel.PendingChoice);
        Assert.Null(playlistService.ActivePlaylistId);
    }

    [Fact]
    public void ConfirmSwitch_SetsActivePlaylistAndCloses()
    {
        Playlist stored = playlistService.CreatePlaylist("Custom", [1]);
        PlaylistSelectionViewModel viewModel = CreateLoadedViewModel();
        bool closed = false;
        viewModel.Closed += (_, _) => closed = true;

        viewModel.SelectCommand.Execute(viewModel.Choices[1]);
        viewModel.ConfirmSwitchCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal(stored.Id, playlistService.ActivePlaylistId);
        Assert.Null(viewModel.PendingChoice);
    }

    [Fact]
    public void CancelSwitch_KeepsActivePlaylist()
    {
        Playlist stored = playlistService.CreatePlaylist("Custom", [1]);
        PlaylistSelectionViewModel viewModel = CreateLoadedViewModel();

        viewModel.SelectCommand.Execute(viewModel.Choices[1]);
        viewModel.CancelSwitchCommand.Execute(null);

        Assert.Null(viewModel.PendingChoice);
        Assert.Null(playlistService.ActivePlaylistId);
    }

    [Fact]
    public void Select_ActiveChoice_JustCloses()
    {
        PlaylistSelectionViewModel viewModel = CreateLoadedViewModel();
        bool closed = false;
        viewModel.Closed += (_, _) => closed = true;

        viewModel.SelectCommand.Execute(viewModel.Choices[0]);

        Assert.True(closed);
        Assert.Null(viewModel.PendingChoice);
    }

    [Fact]
    public void Load_CountsOnlyNonDeletedSongs()
    {
        Playlist stored = playlistService.CreatePlaylist("Custom", [1, 2]);
        playlistService.SetItemDeleted(stored.Id, 1, true);

        PlaylistSelectionViewModel viewModel = CreateLoadedViewModel();

        Assert.Equal(1, viewModel.Choices[1].SongCount);
    }
}
