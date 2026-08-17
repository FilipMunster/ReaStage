using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReaStage.ViewModels;

namespace ReaStage.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();

        // The captured key itself is taken by the window handler. These only stop
        // the keystroke from also reaching a text box that happens to hold focus.
        AddHandler(KeyUpEvent, OnCaptureSwallow, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnCaptureSwallow, RoutingStrategies.Tunnel);
    }

    private void OnCaptureSwallow(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel && viewModel.IsCapturing)
        {
            e.Handled = true;
        }
    }
}
