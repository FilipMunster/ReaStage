using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReaStage.ViewModels;

namespace ReaStage.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Tunnel handler so transport keys work regardless of focused control
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.HandleKey(e))
        {
            e.Handled = true;
        }
    }
}