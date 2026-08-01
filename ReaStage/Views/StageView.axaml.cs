using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using ReaStage.ViewModels;

namespace ReaStage.Views;

public partial class StageView : UserControl
{
    public StageView()
    {
        InitializeComponent();
    }

    private void OnCurrentSongTapped(object? sender, TappedEventArgs e)
    {
        // The prev/next buttons live inside the card, so their taps must not also
        // toggle playback
        if (e.Source is Visual source && source.FindAncestorOfType<Button>(includeSelf: true) is not null)
        {
            return;
        }

        if (DataContext is StageViewModel viewModel)
        {
            viewModel.PlayPauseCommand.Execute(null);
        }
    }

    private void OnSongTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control control && control.DataContext is StageSongItem item && DataContext is StageViewModel viewModel)
        {
            viewModel.ArmOrJumpCommand.Execute(item);
        }
    }
}
