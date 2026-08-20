using System;
using System.Collections.Generic;
using System.Text;

namespace ReaStage.Core;

public enum ReaperPlayState
{
    Stopped = 0,
    Playing = 1,
    Paused = 2,
    Recording = 5,
    RecordPaused = 6
}
