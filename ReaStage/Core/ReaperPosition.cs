namespace ReaStage.Core;

public readonly record struct ReaperPosition(
    ReaperPlayState PlayState,
    double PositionSeconds,
    double FullBeatPosition,
    int MeasureCount,
    double BeatsInMeasure,
    int TimeSigNumerator,
    int TimeSigDenominator,
    bool IsRepeatOn,
    string PositionString,       // Formát dle projektu (např. čas)
    string PositionStringBeats,  // Formát Measures.Beats.Hundredths
    double TempoBpm              // Aktuální tempo v BPM
);