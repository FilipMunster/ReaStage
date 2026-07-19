using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ReaStage.ViewModels;
using System.Threading.Tasks;

namespace ReaStage.Views;

public partial class PlaylistEditorView : UserControl
{
    private static readonly DataFormat<PlaylistEditorViewModel.SongItemViewModel> SongDataFormat =
        DataFormat.CreateInProcessFormat<PlaylistEditorViewModel.SongItemViewModel>("reastage-song");

    public PlaylistEditorView()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private async void Song_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control
            && control.DataContext is PlaylistEditorViewModel.SongItemViewModel song
            && song.CanDrag
            && e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
        {
            DataTransfer data = new DataTransfer();
            data.Add(DataTransferItem.Create(SongDataFormat, song));
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = ResolveDropAction(e) is not null ? DragDropEffects.Move : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not PlaylistEditorViewModel viewModel)
        {
            return;
        }

        (PlaylistEditorViewModel.SongItemViewModel source, object target)? action = ResolveDropAction(e);
        if (action is null)
        {
            return;
        }

        if (action.Value.target is PlaylistEditorViewModel.SongItemViewModel targetSong)
        {
            viewModel.MoveSong(action.Value.source, targetSong);
        }
        else if (action.Value.target is PlaylistEditorViewModel.PlaylistColumnViewModel column)
        {
            viewModel.MoveSongToEnd(action.Value.source, column);
        }

        e.Handled = true;
    }

    // Returns the dragged song and the drop target (song or column), null when the drop is invalid
    private static (PlaylistEditorViewModel.SongItemViewModel source, object target)? ResolveDropAction(DragEventArgs e)
    {
        if (e.DataTransfer.TryGetValue(SongDataFormat) is not PlaylistEditorViewModel.SongItemViewModel source)
        {
            return null;
        }

        object? context = (e.Source as StyledElement)?.DataContext;

        if (context is PlaylistEditorViewModel.SongItemViewModel targetSong)
        {
            bool valid = targetSong.PlaylistId == source.PlaylistId
                && !targetSong.IsDeleted
                && targetSong.ItemIndex != source.ItemIndex;
            return valid ? (source, targetSong) : null;
        }

        if (context is PlaylistEditorViewModel.PlaylistColumnViewModel column)
        {
            return column.Id == source.PlaylistId ? (source, column) : null;
        }

        return null;
    }
}
