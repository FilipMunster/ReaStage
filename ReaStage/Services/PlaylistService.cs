using Microsoft.Extensions.Logging;
using ReaStage.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ReaStage.Services;

internal class PlaylistService : IPlaylistService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly ILogger<PlaylistService> logger;
    private readonly string databasePath;
    private readonly PlaylistDatabase database;

    public event EventHandler? PlaylistsChanged;

    public IReadOnlyList<Playlist> Playlists => database.Playlists;

    public Guid? ActivePlaylistId
    {
        get => database.ActivePlaylistId;
        set
        {
            if (value is not null && GetPlaylist(value.Value) is null)
            {
                throw new ArgumentException($"Playlist {value} does not exist.", nameof(value));
            }

            if (database.ActivePlaylistId != value)
            {
                database.ActivePlaylistId = value;
                OnChanged();
            }
        }
    }

    public PlaylistService(ILogger<PlaylistService> logger)
        : this(logger, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReaStage", "playlists.json"))
    {
    }

    // Test constructor with an explicit database path
    internal PlaylistService(ILogger<PlaylistService> logger, string databasePath)
    {
        this.logger = logger;
        this.databasePath = databasePath;
        database = Load();
    }

    public Playlist? GetPlaylist(Guid id)
    {
        return database.Playlists.FirstOrDefault(p => p.Id == id);
    }

    public Playlist CreatePlaylist(string name, IEnumerable<int> regionIds)
    {
        Playlist playlist = new Playlist
        {
            Name = name,
            Items = regionIds.Select(id => new PlaylistItem { RegionId = id }).ToList()
        };
        database.Playlists.Add(playlist);
        OnChanged();
        return playlist;
    }

    public Playlist DuplicatePlaylist(Guid id)
    {
        Playlist source = RequirePlaylist(id);
        Playlist copy = new Playlist
        {
            Name = $"{source.Name} (kopie)",
            Items = source.Items.Select(i => new PlaylistItem { RegionId = i.RegionId, Deleted = i.Deleted }).ToList()
        };
        database.Playlists.Add(copy);
        OnChanged();
        return copy;
    }

    public void RenamePlaylist(Guid id, string name)
    {
        RequirePlaylist(id).Name = name;
        OnChanged();
    }

    public void MoveItem(Guid playlistId, int fromIndex, int toIndex)
    {
        Playlist playlist = RequirePlaylist(playlistId);
        PlaylistItem item = playlist.Items[fromIndex];
        playlist.Items.RemoveAt(fromIndex);
        playlist.Items.Insert(toIndex, item);
        OnChanged();
    }

    public void SetItemDeleted(Guid playlistId, int itemIndex, bool deleted)
    {
        RequirePlaylist(playlistId).Items[itemIndex].Deleted = deleted;
        OnChanged();
    }

    private Playlist RequirePlaylist(Guid id)
    {
        return GetPlaylist(id) ?? throw new ArgumentException($"Playlist {id} does not exist.", nameof(id));
    }

    // Every mutation autosaves immediately
    private void OnChanged()
    {
        Save();
        PlaylistsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
            File.WriteAllText(databasePath, JsonSerializer.Serialize(database, JsonOptions));
            logger.LogDebug("Playlists saved to {Path}", databasePath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save playlists to {Path}", databasePath);
        }
    }

    private PlaylistDatabase Load()
    {
        try
        {
            if (File.Exists(databasePath))
            {
                PlaylistDatabase? loaded = JsonSerializer.Deserialize<PlaylistDatabase>(File.ReadAllText(databasePath), JsonOptions);
                if (loaded is not null)
                {
                    logger.LogDebug("Playlists loaded from {Path}", databasePath);
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load playlists from {Path}, starting with an empty database", databasePath);
        }

        return new PlaylistDatabase();
    }
}
