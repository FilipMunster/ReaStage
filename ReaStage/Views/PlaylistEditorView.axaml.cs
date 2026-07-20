using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using ReaStage.ViewModels;
using System.Linq;
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
        if (DataContext is not PlaylistEditorViewModel viewModel
            || sender is not Control control
            || control.DataContext is not PlaylistEditorViewModel.SongItemViewModel song
            || !song.CanDrag
            || !e.GetCurrentPoint(control).Properties.IsLeftButtonPressed
            || IsFromButton(e.Source))
        {
            return;
        }

        song.IsDragging = true;
        ShowGhost(song.Name, e.GetPosition(GhostLayer));

        DataTransfer data = new DataTransfer();
        data.Add(DataTransferItem.Create(SongDataFormat, song));

        DragDropEffects result;
        try
        {
            result = await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
        }
        finally
        {
            HideGhost();
            song.IsDragging = false;
        }

        // A successful drop is committed in OnDrop; anything else reverts the preview
        if (result == DragDropEffects.None)
        {
            viewModel.CancelDrag();
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        MoveGhost(e.GetPosition(GhostLayer));

        if (DataContext is not PlaylistEditorViewModel viewModel
            || e.DataTransfer.TryGetValue(SongDataFormat) is not PlaylistEditorViewModel.SongItemViewModel source)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        Border? columnBorder = FindColumnBorder(e.Source as Visual);
        if (columnBorder?.DataContext is not PlaylistEditorViewModel.PlaylistColumnViewModel column
            || column.Id != source.PlaylistId)
        {
            // Not a valid target column (empty area, header, or a different playlist)
            e.DragEffects = DragDropEffects.None;
            return;
        }

        int targetIndex = ComputeTargetIndex(columnBorder, column, source, e);
        if (targetIndex >= 0)
        {
            viewModel.PreviewMoveToIndex(source, targetIndex);
        }

        e.DragEffects = DragDropEffects.Move;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is PlaylistEditorViewModel viewModel
            && e.DataTransfer.TryGetValue(SongDataFormat) is PlaylistEditorViewModel.SongItemViewModel source)
        {
            viewModel.CommitDrag(source);
            e.Handled = true;
        }
    }

    // Insertion index = number of non-dragged cards whose vertical center is above the cursor.
    // Using centers (not edges) and excluding the dragged card gives half-a-card of hysteresis,
    // so a stationary cursor always resolves to the same index and the list stops oscillating.
    private static int ComputeTargetIndex(
        Border columnBorder,
        PlaylistEditorViewModel.PlaylistColumnViewModel column,
        PlaylistEditorViewModel.SongItemViewModel source,
        DragEventArgs e)
    {
        ItemsControl? list = columnBorder.GetVisualDescendants()
            .OfType<ItemsControl>()
            .FirstOrDefault(ic => ReferenceEquals(ic.ItemsSource, column.Songs));
        if (list is null)
        {
            return -1;
        }

        double cursorY = e.GetPosition(columnBorder).Y;
        int targetIndex = 0;
        for (int i = 0; i < column.Songs.Count; i++)
        {
            if (ReferenceEquals(column.Songs[i], source))
            {
                continue;
            }

            Control? container = list.ContainerFromIndex(i);
            if (container is null)
            {
                continue;
            }

            Point? center = container.TranslatePoint(new Point(0, container.Bounds.Height / 2), columnBorder);
            if (center is Point point && point.Y < cursorY)
            {
                targetIndex++;
            }
        }

        return targetIndex;
    }

    private static Border? FindColumnBorder(Visual? from)
    {
        for (Visual? current = from; current is not null; current = current.GetVisualParent())
        {
            if (current is Border border && border.Classes.Contains("column"))
            {
                return border;
            }
        }

        return null;
    }

    private static bool IsFromButton(object? eventSource)
    {
        for (StyledElement? element = eventSource as StyledElement; element is not null; element = element.Parent)
        {
            if (element is Button)
            {
                return true;
            }
        }

        return false;
    }

    private void ShowGhost(string text, Point position)
    {
        DragGhostText.Text = text;
        DragGhost.IsVisible = true;
        MoveGhost(position);
    }

    private void MoveGhost(Point position)
    {
        Canvas.SetLeft(DragGhost, position.X + 14);
        Canvas.SetTop(DragGhost, position.Y + 10);
    }

    private void HideGhost()
    {
        DragGhost.IsVisible = false;
    }
}
