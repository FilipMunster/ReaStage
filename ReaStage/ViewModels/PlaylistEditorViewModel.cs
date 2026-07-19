using CommunityToolkit.Mvvm.Input;
using System;

namespace ReaStage.ViewModels;

// Placeholder — the actual editor is Phase 6 of the implementation plan
public partial class PlaylistEditorViewModel : ViewModelBase
{
    public event EventHandler? Closed;

    [RelayCommand]
    private void Back()
    {
        Closed?.Invoke(this, EventArgs.Empty);
    }
}
