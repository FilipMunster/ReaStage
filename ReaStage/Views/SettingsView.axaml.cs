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

        // Tunnel so a captured key is taken before anything else reacts to it
        AddHandler(KeyDownEvent, OnCaptureKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnCaptureSwallow, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnCaptureSwallow, RoutingStrategies.Tunnel);
    }

    // While capturing, no other control may see the keystroke — otherwise the key
    // being assigned also lands in whichever text box holds focus
    private void OnCaptureSwallow(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel && viewModel.IsCapturing)
        {
            e.Handled = true;
        }
    }

    private void OnCaptureKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel || !viewModel.IsCapturing)
        {
            return;
        }

        // Wait for a real key; Escape (cancel) is handled at the window level
        if (e.Key == Key.Escape || IsModifierKey(e.Key))
        {
            return;
        }

        viewModel.ApplyCapturedKey(new KeyGesture(e.Key, e.KeyModifiers));
        e.Handled = true;
    }

    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin;
    }
}
