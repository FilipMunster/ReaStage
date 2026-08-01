using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ReaStage.ViewModels;
using System;
using System.Diagnostics;

namespace ReaStage.Views;

public partial class StageView : UserControl
{
    // Rate-limit seeks while dragging; the final position is always sent on release
    private const long ScrubThrottleMs = 60;

    private readonly Stopwatch scrubClock = Stopwatch.StartNew();
    private bool isScrubbing;
    private long lastScrubMs;

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

    private void OnScrubPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(CurrentFrame).Properties.IsLeftButtonPressed)
        {
            return;
        }

        isScrubbing = true;
        e.Pointer.Capture(CurrentFrame);
        SendScrub(e, force: true);
    }

    private void OnScrubMoved(object? sender, PointerEventArgs e)
    {
        if (isScrubbing)
        {
            SendScrub(e, force: false);
        }
    }

    private void OnScrubReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!isScrubbing)
        {
            return;
        }

        isScrubbing = false;
        e.Pointer.Capture(null);
        SendScrub(e, force: true);
    }

    private void SendScrub(PointerEventArgs e, bool force)
    {
        if (DataContext is not StageViewModel viewModel)
        {
            return;
        }

        double width = CurrentFrame.Bounds.Width;
        if (width <= 0)
        {
            return;
        }

        long now = scrubClock.ElapsedMilliseconds;
        if (!force && now - lastScrubMs < ScrubThrottleMs)
        {
            return;
        }

        lastScrubMs = now;
        double fraction = Math.Clamp(e.GetPosition(CurrentFrame).X / width, 0, 1);
        _ = viewModel.ScrubToAsync(fraction);
    }
}
