using CommunityToolkit.Mvvm.ComponentModel;

namespace ReaStage.ViewModels;

// A song shown around the current one in the stage view; Index is the position
// in the active playlist, used for two-click jump.
public partial class StageSongItem : ObservableObject
{
    public StageSongItem(int index, string name)
    {
        Index = index;
        Name = name;
    }

    public int Index { get; }

    public string Name { get; }

    [ObservableProperty]
    private bool isArmed;
}
