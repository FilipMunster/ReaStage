using ReaStage.Services;

namespace ReaStage.Tests;

public class ReaperClientTests
{
    [Fact]
    public void ParseBeatPosition_MatchesBeatposFromReaper()
    {
        // Live sample from REAPER: BEATPOS reported full_beat 2343.6389 while
        // /beat/str reported "586.4.64" at 4/4
        double? beats = ReaperClient.ParseBeatPosition("586.4.64", 4);

        Assert.NotNull(beats);
        Assert.Equal(2343.64, beats!.Value, 2);
    }

    [Fact]
    public void ParseBeatPosition_ProjectStartIsZero()
    {
        Assert.Equal(0, ReaperClient.ParseBeatPosition("1.1.00", 4));
    }

    [Fact]
    public void ParseBeatPosition_WithoutHundredths_StillParses()
    {
        Assert.Equal(4, ReaperClient.ParseBeatPosition("2.1", 4));
    }

    [Theory]
    [InlineData("", 4)]
    [InlineData("nonsense", 4)]
    [InlineData("586.4.64", 0)] // time signature not known yet
    public void ParseBeatPosition_InvalidInput_ReturnsNull(string value, int beatsPerMeasure)
    {
        Assert.Null(ReaperClient.ParseBeatPosition(value, beatsPerMeasure));
    }
}
