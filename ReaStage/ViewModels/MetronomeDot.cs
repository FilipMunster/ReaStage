using CommunityToolkit.Mvvm.ComponentModel;

namespace ReaStage.ViewModels;

// One beat of the current bar. The instances are reused between updates so a beat
// change only flips IsLit — rebuilding the row would cost latency the metronome
// cannot afford.
public partial class MetronomeDot : ObservableObject
{
    public MetronomeDot(bool isDownbeat)
    {
        IsDownbeat = isDownbeat;
    }

    public bool IsDownbeat { get; }

    [ObservableProperty]
    private bool isLit;
}
