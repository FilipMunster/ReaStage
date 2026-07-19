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

    public KeySettings Keys { get; set; } = new();
    public int PreviousSongsShown { get; set; } = 2;
    public int NextSongsShown { get; set; } = 3;
    public double PreviousThresholdSeconds { get; set; } = 3.0;
    public int RegionsPollSeconds { get; set; } = 10;
    public ReaperSettings Reaper { get; set; } = new();
}
