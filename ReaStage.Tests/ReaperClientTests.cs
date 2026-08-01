using LucHeart.CoreOSC;
using ReaStage.Services;

namespace ReaStage.Tests;

public class ReaperClientTests
{
    // REAPER groups simultaneous feedback into a bundle but sends a lone value as a
    // bare message; only accepting bundles used to drop those (e.g. /tempo/raw)
    [Fact]
    public void ParseOscPacket_BareMessage_IsAccepted()
    {
        byte[] packet = new OscMessage("/tempo/raw", 174f).GetBytes();

        IReadOnlyList<OscMessage> messages = ReaperClient.ParseOscPacket(packet);

        Assert.Single(messages);
        Assert.Equal("/tempo/raw", messages[0].Address);
        Assert.Equal(174f, messages[0].Arguments[0]);
    }

    [Fact]
    public void ParseOscPacket_Bundle_ReturnsEveryMessage()
    {
        byte[] packet = new OscBundle(0,
        [
            new OscMessage("/time", 12.5f),
            new OscMessage("/beat/str", "586.4.64")
        ]).GetBytes();

        IReadOnlyList<OscMessage> messages = ReaperClient.ParseOscPacket(packet);

        Assert.Equal(["/time", "/beat/str"], messages.Select(m => m.Address));
    }

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
