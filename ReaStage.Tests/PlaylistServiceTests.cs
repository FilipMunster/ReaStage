using Microsoft.Extensions.Logging.Abstractions;
using ReaStage.Core;
using ReaStage.Services;

namespace ReaStage.Tests;

public class PlaylistServiceTests : IDisposable
{
    private readonly string databasePath;

    public PlaylistServiceTests()
    {
        databasePath = Path.Combine(Path.GetTempPath(), "ReaStageTests", $"{Guid.NewGuid()}.json");
    }

    public void Dispose()
    {
        if (File.Exists(databasePath))
        {
            File.Delete(databasePath);
        }
    }

    private PlaylistService CreateService()
    {
        return new PlaylistService(NullLogger<PlaylistService>.Instance, databasePath);
    }

    [Fact]
    public void CreatePlaylist_PersistsToDisk()
    {
        PlaylistService service = CreateService();
        Playlist created = service.CreatePlaylist("Test", [3, 1, 7]);

        PlaylistService reloaded = CreateService();

        Playlist? loaded = reloaded.GetPlaylist(created.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Test", loaded.Name);
        Assert.Equal([3, 1, 7], loaded.Items.Select(i => i.RegionId));
        Assert.All(loaded.Items, i => Assert.False(i.Deleted));
    }

    [Fact]
    public void ActivePlaylistId_Persists()
    {
        PlaylistService service = CreateService();
        Playlist created = service.CreatePlaylist("Test", [1]);
        service.ActivePlaylistId = created.Id;

        PlaylistService reloaded = CreateService();

        Assert.Equal(created.Id, reloaded.ActivePlaylistId);
    }

    [Fact]
    public void ActivePlaylistId_Null_MeansReaperPlaylist()
    {
        PlaylistService service = CreateService();
        Playlist created = service.CreatePlaylist("Test", [1]);
        service.ActivePlaylistId = created.Id;
        service.ActivePlaylistId = null;

        PlaylistService reloaded = CreateService();

        Assert.Null(reloaded.ActivePlaylistId);
    }

    [Fact]
    public void ActivePlaylistId_UnknownId_Throws()
    {
        PlaylistService service = CreateService();

        Assert.Throws<ArgumentException>(() => service.ActivePlaylistId = Guid.NewGuid());
    }

    [Fact]
    public void MoveItem_ReordersAndPersists()
    {
        PlaylistService service = CreateService();
        Playlist created = service.CreatePlaylist("Test", [1, 2, 3]);

        service.MoveItem(created.Id, 0, 2);

        PlaylistService reloaded = CreateService();
        Assert.Equal([2, 3, 1], reloaded.GetPlaylist(created.Id)!.Items.Select(i => i.RegionId));
    }

    [Fact]
    public void SetItemDeleted_FlagsItemAndPersists()
    {
        PlaylistService service = CreateService();
        Playlist created = service.CreatePlaylist("Test", [1, 2]);

        service.SetItemDeleted(created.Id, 1, true);

        PlaylistService reloaded = CreateService();
        List<PlaylistItem> items = reloaded.GetPlaylist(created.Id)!.Items;
        Assert.False(items[0].Deleted);
        Assert.True(items[1].Deleted);
    }

    [Fact]
    public void SetItemDeleted_Restore_ClearsFlag()
    {
        PlaylistService service = CreateService();
        Playlist created = service.CreatePlaylist("Test", [1]);
        service.SetItemDeleted(created.Id, 0, true);

        service.SetItemDeleted(created.Id, 0, false);

        Assert.False(service.GetPlaylist(created.Id)!.Items[0].Deleted);
    }

    [Fact]
    public void DuplicatePlaylist_CreatesIndependentCopy()
    {
        PlaylistService service = CreateService();
        Playlist source = service.CreatePlaylist("Test", [1, 2]);
        service.SetItemDeleted(source.Id, 1, true);

        Playlist copy = service.DuplicatePlaylist(source.Id);

        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal([1, 2], copy.Items.Select(i => i.RegionId));
        Assert.True(copy.Items[1].Deleted);

        // Mutating the copy must not affect the source
        service.MoveItem(copy.Id, 0, 1);
        Assert.Equal([1, 2], service.GetPlaylist(source.Id)!.Items.Select(i => i.RegionId));
    }

    [Fact]
    public void DeletePlaylist_RemovesAndPersists()
    {
        PlaylistService service = CreateService();
        Playlist a = service.CreatePlaylist("A", [1]);
        Playlist b = service.CreatePlaylist("B", [2]);

        service.DeletePlaylist(a.Id);

        PlaylistService reloaded = CreateService();
        Assert.Null(reloaded.GetPlaylist(a.Id));
        Assert.NotNull(reloaded.GetPlaylist(b.Id));
    }

    [Fact]
    public void DeletePlaylist_ActiveOne_ResetsActiveToReaper()
    {
        PlaylistService service = CreateService();
        Playlist a = service.CreatePlaylist("A", [1]);
        service.ActivePlaylistId = a.Id;

        service.DeletePlaylist(a.Id);

        PlaylistService reloaded = CreateService();
        Assert.Null(reloaded.ActivePlaylistId);
    }

    [Fact]
    public void DeletePlaylist_KeepsOtherActive()
    {
        PlaylistService service = CreateService();
        Playlist a = service.CreatePlaylist("A", [1]);
        Playlist b = service.CreatePlaylist("B", [2]);
        service.ActivePlaylistId = b.Id;

        service.DeletePlaylist(a.Id);

        Assert.Equal(b.Id, service.ActivePlaylistId);
    }

    [Fact]
    public void DeletePlaylist_UnknownId_Throws()
    {
        PlaylistService service = CreateService();

        Assert.Throws<ArgumentException>(() => service.DeletePlaylist(Guid.NewGuid()));
    }

    [Fact]
    public void RenamePlaylist_Persists()
    {
        PlaylistService service = CreateService();
        Playlist created = service.CreatePlaylist("Old", [1]);

        service.RenamePlaylist(created.Id, "New");

        PlaylistService reloaded = CreateService();
        Assert.Equal("New", reloaded.GetPlaylist(created.Id)!.Name);
    }

    [Fact]
    public void Load_CorruptedFile_StartsWithEmptyDatabase()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        File.WriteAllText(databasePath, "{ not valid json");

        PlaylistService service = CreateService();

        Assert.Empty(service.Playlists);
        Assert.Null(service.ActivePlaylistId);
    }

    [Fact]
    public void Mutations_RaisePlaylistsChanged()
    {
        PlaylistService service = CreateService();
        int raised = 0;
        service.PlaylistsChanged += (_, _) => raised++;

        Playlist created = service.CreatePlaylist("Test", [1, 2]);
        service.RenamePlaylist(created.Id, "Renamed");
        service.MoveItem(created.Id, 0, 1);
        service.SetItemDeleted(created.Id, 0, true);
        service.ActivePlaylistId = created.Id;

        Assert.Equal(5, raised);
    }
}
