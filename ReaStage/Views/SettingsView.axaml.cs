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

        // Tunnel so a captured key is taken before a focused button treats it as a click
        AddHandler(KeyDownEvent, OnCaptureKeyDown, RoutingStrategies.Tunnel);
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
