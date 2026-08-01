using Avalonia.Controls;
using Avalonia.Input;
using ReaStage.ViewModels;

namespace ReaStage.Views;

public partial class StageView : UserControl
{
    public StageView()
    {
        InitializeComponent();
    }

    private void OnSongTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control control && control.DataContext is StageSongItem item && DataContext is StageViewModel viewModel)
        {
            viewModel.ArmOrJumpCommand.Execute(item);
        }
    }
}
