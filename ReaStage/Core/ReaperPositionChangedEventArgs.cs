using System;
using System.Collections.Generic;
using System.Text;

namespace ReaStage.Core;

public class ReaperPositionChangedEventArgs(ReaperPosition position) : EventArgs
{
    public ReaperPosition Position { get; } = position;
}
