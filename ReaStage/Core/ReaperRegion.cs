using System;
using System.Collections.Generic;
using System.Text;

namespace ReaStage.Core;

public readonly record struct ReaperRegion(
    int Id,
    string Name,
    double StartPosition,
    double EndPosition,
    uint? Color // Barva je volitelná a v hex formátu 0xaarrggbb
);