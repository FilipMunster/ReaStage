namespace ReaStage.Core;

public class AppSettings
{
    public class KeySettings
    {
        public string PlayPause { get; set; } = "Space";
        public string Previous { get; set; } = "Left";
        public string Next { get; set; } = "Right";
    }

    public class ReaperSettings
    {
        public string Host { get; set; } = "localhost";
        public int HttpPort { get; set; } = 9123;
        public int OscPort { get; set; } = 9124;
    }

    public class WebSettings
    {
        public bool Enabled { get; set; } = false;
        public int Port { get; set; } = 9125;
    }

    public KeySettings Keys { get; set; } = new();
    public int PreviousSongsShown { get; set; } = 2;
    public int NextSongsShown { get; set; } = 3;
    public double PreviousThresholdSeconds { get; set; } = 3.0;
    public int RegionsPollSeconds { get; set; } = 10;

    // Font size multiplier for the stage view only, in percent (50–200)
    public int FontScalePercent { get; set; } = 100;

    public ReaperSettings Reaper { get; set; } = new();
    public WebSettings Web { get; set; } = new();
}
