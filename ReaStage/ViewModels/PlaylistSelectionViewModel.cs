using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReaStage.Core;
using ReaStage.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ReaStage.ViewModels;

public partial class PlaylistSelectionViewModel : ViewModelBase
{
    public class PlaylistChoice
    {
        // null = the virtual REAPER playlist
        public Guid? Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public int SongCount { get; init; }
        public bool IsActive { get; init; }

        public string Display => $"{Name}  ({SongCount})";
    }

    private readonly IPlaylistService playlistService;
    private readonly IRegionCatalog regionCatalog;

    public event EventHandler? Closed;

    [ObservableProperty]
    private IReadOnlyList<PlaylistChoice> choices = [];

    [ObservableProperty]
    private PlaylistChoice? pendingChoice;

    public PlaylistSelectionViewModel(IPlaylistService playlistService, IRegionCatalog regionCatalog)
    {
        this.playlistService = playlistService;
        this.regionCatalog = regionCatalog;
    }

    // Called every time the selection page is shown
    public void Load()
    {
        Guid? activeId = playlistService.ActivePlaylistId;

        List<PlaylistChoice> list =
        [
            new PlaylistChoice
            {
                Id = null,
                Name = "REAPER (dle timeline)",
                SongCount = regionCatalog.Regions.Count,
                IsActive = activeId is null
            }
        ];

        list.AddRange(playlistService.Playlists.Select(p => new PlaylistChoice
        {
            Id = p.Id,
            Name = p.Name,
            SongCount = p.Items.Count(i => !i.Deleted),
            IsActive = p.Id == activeId
        }));

        Choices = list;
        PendingChoice = null;
    }

    [RelayCommand]
    private void Select(PlaylistChoice choice)
    {
        if (choice.IsActive)
        {
            Closed?.Invoke(this, EventArgs.Empty);
            return;
        }

        PendingChoice = choice;
    }

    [RelayCommand]
    private void ConfirmSwitch()
    {
        if (PendingChoice is null)
        {
            return;
        }

        playlistService.ActivePlaylistId = PendingChoice.Id;
        PendingChoice = null;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void CancelSwitch()
    {
        PendingChoice = null;
    }

    [RelayCommand]
    private void Back()
    {
        PendingChoice = null;
        Closed?.Invoke(this, EventArgs.Empty);
    }
}
