using Microsoft.Extensions.Logging;
using ReaStage.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LucHeart.CoreOSC;
using System.Net;
using System.Diagnostics;

namespace ReaStage.Services;

internal class ReaperClient : IReaperClient, IDisposable
{
    // /beat/str carries only hundredths of a beat, so tempo is measured over a window
    // long enough to make that quantisation negligible
    private const double TempoWindowSeconds = 1.5;

    // A longer gap means OSC packets were missed or the cursor was moved
    private const double TempoWindowMaxSeconds = 5.0;

    private const double TempoMinBpm = 20;
    private const double TempoMaxBpm = 400;

    private readonly string baseUrl;
    private readonly HttpClient httpClient;
    private readonly UdpClient oscSocket;
    private readonly CancellationTokenSource cts = new();
    private readonly ILogger<ReaperClient> logger;
    private ReaperPosition? lastPosition;
    private bool disposedValue;

    // Tempo measurement state (see WithDerivedTempo)
    private bool tempoReportedByReaper;
    private double tempoAnchorSeconds = double.NaN;
    private double tempoAnchorBeats;

    public event EventHandler<ReaperPositionChangedEventArgs>? PositionChanged;

    public ReaperClient(ILogger<ReaperClient> logger, ISettingsService settingsService)
    {
        this.logger = logger;

        AppSettings.ReaperSettings reaperSettings = settingsService.Settings.Reaper;
        baseUrl = $"http://{reaperSettings.Host}:{reaperSettings.HttpPort}/_/";
        httpClient = new HttpClient();
        IPEndPoint endpoint = new IPEndPoint(IPAddress.Any, reaperSettings.OscPort);
        oscSocket = new UdpClient(endpoint);

        _ = ListenOscAsync(cts.Token);
    }

    public async Task SendPlayPause() => await SendCommand("40073"); // ID pro Transport: Play/pause

    public async Task SendStop() => await SendCommand("1016"); // ID pro Transport: Stop

    public async Task SendPlay() => await SendCommand("1007"); // ID pro Transport: Play

    public async Task SetPosition(double seconds) => await SendCommand($"SET/POS/{seconds.ToString(CultureInfo.InvariantCulture)}");

    // Chained in a single HTTP request so stop and seek happen atomically in REAPER
    public async Task StopAndSetPosition(double seconds) => await SendCommand($"1016;SET/POS/{seconds.ToString(CultureInfo.InvariantCulture)}");

    public async Task<List<ReaperRegion>> GetRegions()
    {
        var response = await httpClient.GetStringAsync($"{baseUrl}REGION");
        var regions = new List<ReaperRegion>();

        var lines = response.Split('\n');
        foreach (var line in lines)
        {
            var tokens = line.Split('\t');
            // Formát: REGION \t name \t ID \t start \t end [\t color]
            if (tokens.Length >= 5 && tokens[0] == "REGION")
            {
                uint? color = null;
                if (tokens.Length >= 6 && uint.TryParse(tokens[5].Replace("0x", ""), NumberStyles.HexNumber, null, out var parsedColor))
                {
                    color = parsedColor;
                }

                regions.Add(new ReaperRegion(
                    Id: int.Parse(tokens[2]),
                    Name: Unescape(tokens[1]),
                    StartPosition: Math.Round(double.Parse(tokens[3], CultureInfo.InvariantCulture), Constants.TIME_ROUND_PRECISION),
                    EndPosition: Math.Round(double.Parse(tokens[4], CultureInfo.InvariantCulture), Constants.TIME_ROUND_PRECISION),
                    Color: color
                ));
            }
        }
        return regions;
    }

    public async Task<ReaperPosition> GetPosition()
    {
        // Vyžádáme si TRANSPORT i BEATPOS najednou pro konzistenci
        var response = await httpClient.GetStringAsync($"{baseUrl}TRANSPORT;BEATPOS");
        var lines = response.Split('\n');

        // Pomocné proměnné pro složení výsledné struktury
        var transportTokens = lines.FirstOrDefault(l => l.StartsWith("TRANSPORT"))?.Split('\t');
        var beatposTokens = lines.FirstOrDefault(l => l.StartsWith("BEATPOS"))?.Split('\t');

        if (transportTokens == null || beatposTokens == null)
            throw new InvalidOperationException("Failed to get position data from REAPER.");

        // Parsování TRANSPORT (indexy dle main.js: playstate(1), pos(2), repeat(3), pos_str(4), pos_str_beats(5))
        var playState = (ReaperPlayState)int.Parse(transportTokens[1]);
        var posSec = double.Parse(transportTokens[2], CultureInfo.InvariantCulture);
        var isRepeat = transportTokens[3] != "0";
        var posStr = Unescape(transportTokens[4]);
        var posStrBeats = Unescape(transportTokens[5]);

        // Parsování BEATPOS (indexy: full_beat(3), meas(4), beats_in_meas(5), ts_num(6), ts_den(7))
        var fullBeat = double.Parse(beatposTokens[3], CultureInfo.InvariantCulture);
        var measure = int.Parse(beatposTokens[4]);
        var beatsInMeasure = double.Parse(beatposTokens[5], CultureInfo.InvariantCulture);
        var tsNum = int.Parse(beatposTokens[6]);
        var tsDen = int.Parse(beatposTokens[7]);

        // The web API exposes no tempo (neither TRANSPORT nor BEATPOS), so the value
        // known from OSC is carried over — REAPER sends no OSC at all while stopped
        double tempo = lastPosition?.TempoBpm ?? 0;

        var position = new ReaperPosition(
            playState, posSec, fullBeat, measure, beatsInMeasure,
            tsNum, tsDen, isRepeat, posStr, posStrBeats, tempo
        );

        lastPosition = position;
        return position;
    }

    private async Task SendCommand(string command)
    {
        await httpClient.GetAsync($"{baseUrl}{command}");
    }

    private static string Unescape(string value)
    {
        return value.Replace("\\t", "\t").Replace("\\n", "\n").Replace("\\\\", "\\");
    }

    private async Task ListenOscAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult received = await oscSocket.ReceiveAsync(ct);
                await HandleOscPacket(received.Buffer);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "OSC Error: {Message}", ex.Message);
            }
        }
    }

    // REAPER groups simultaneous feedback into a bundle but sends a lone value as a
    // bare message. Listening for bundles only silently dropped those — among them
    // /tempo/raw, which is why no BPM ever arrived.
    private async Task HandleOscPacket(byte[] packet)
    {
        await HandleOscMessages(ParseOscPacket(packet));
    }

    internal static IReadOnlyList<OscMessage> ParseOscPacket(byte[] packet)
    {
        return OscBundle.IsBundle(packet)
            ? OscBundle.ParseBundle(packet).Messages
            : [OscMessage.ParseMessage(packet)];
    }

    private async Task HandleOscMessages(IReadOnlyList<OscMessage> messages)
    {
        ReaperPosition lastPosition = this.lastPosition ?? await GetPosition();
        ReaperPosition currentPosition = lastPosition;

        foreach (var msg in messages)
        {
            HandleOscMessage(msg, ref currentPosition);
        }

        if (!tempoReportedByReaper)
        {
            currentPosition = WithDerivedTempo(currentPosition);
        }

        if (currentPosition != lastPosition)
        {
            this.lastPosition = currentPosition;
            PositionChanged?.Invoke(this, new ReaperPositionChangedEventArgs(currentPosition));
        }
    }

    private void HandleOscMessage(OscMessage msg, ref ReaperPosition currentPosition)
    {
        object?[] args = msg.Arguments;

        switch (msg.Address)
        {
            // Transport stavy (REAPER posílá 1.0f když je stav aktivní)
            case "/transport/play":
                if (args.Length > 0 && args[0] is float f && f > 0)
                {
                    currentPosition = currentPosition with { PlayState = ReaperPlayState.Playing };
                }
                break;

            case "/transport/stop":
                if (args.Length > 0 && args[0] is float f1 && f1 > 0)
                {
                    currentPosition = currentPosition with { PlayState = ReaperPlayState.Stopped };
                }
                break;

            case "/transport/pause":
                if (args.Length > 0 && args[0] is float f2 && f2 > 0)
                {
                    currentPosition = currentPosition with { PlayState = ReaperPlayState.Paused };
                }
                break;

            case "/transport/record":
                if (args.Length > 0 && args[0] is float f3 && f3 > 0)
                {
                    currentPosition = currentPosition with { PlayState = ReaperPlayState.Recording };
                }
                break;

            // Pozice v čase
            case "/time":
                if (args.Length > 0 && args[0] is float sec)
                {
                    currentPosition = currentPosition with { PositionSeconds = sec };
                }
                break;

            case "/time/str":
                if (args.Length > 0 && args[0] is string timeStr)
                {
                    currentPosition = currentPosition with { PositionString = timeStr };
                }
                break;

            case "/beat/str":
                if (args.Length > 0 && args[0] is string beatStr)
                {
                    currentPosition = currentPosition with { PositionStringBeats = beatStr };
                }
                break;

            case "/transport/repeat":
                if (args.Length > 0 && args[0] is float rep)
                {
                    currentPosition = currentPosition with { IsRepeatOn = rep > 0 };
                }
                break;

            case "/tempo/raw":
                if (args.Length > 0 && args[0] is float tempo && tempo > 0)
                {
                    tempoReportedByReaper = true;
                    currentPosition = currentPosition with { TempoBpm = tempo };
                }
                break;
        }
    }

    // REAPER pushes /tempo/raw only when the tempo actually changes, so a session that
    // attaches to an already loaded project never learns it. Measure BPM from how fast
    // the beat position advances during playback instead.
    private ReaperPosition WithDerivedTempo(ReaperPosition position)
    {
        if (position.PlayState != ReaperPlayState.Playing)
        {
            tempoAnchorSeconds = double.NaN;
            return position;
        }

        if (ParseBeatPosition(position.PositionStringBeats, position.TimeSigNumerator) is not double beats)
        {
            return position;
        }

        if (double.IsNaN(tempoAnchorSeconds))
        {
            tempoAnchorSeconds = position.PositionSeconds;
            tempoAnchorBeats = beats;
            return position;
        }

        double elapsed = position.PositionSeconds - tempoAnchorSeconds;
        if (elapsed < TempoWindowSeconds)
        {
            return position;
        }

        double advanced = beats - tempoAnchorBeats;
        tempoAnchorSeconds = position.PositionSeconds;
        tempoAnchorBeats = beats;

        if (elapsed > TempoWindowMaxSeconds || advanced <= 0)
        {
            return position;
        }

        // Measured precision does not justify finer steps than half a BPM
        double bpm = Math.Round(60.0 * advanced / elapsed * 2, MidpointRounding.AwayFromZero) / 2;

        return bpm is >= TempoMinBpm and <= TempoMaxBpm
            ? position with { TempoBpm = bpm }
            : position;
    }

    // "measures.beats.hundredths" (1-based) -> absolute beats from the project start
    internal static double? ParseBeatPosition(string value, int beatsPerMeasure)
    {
        if (beatsPerMeasure <= 0 || string.IsNullOrEmpty(value))
        {
            return null;
        }

        string[] parts = value.Split('.');
        if (parts.Length < 2
            || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int measure)
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int beat))
        {
            return null;
        }

        double fraction = 0;
        if (parts.Length > 2 && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hundredths))
        {
            fraction = hundredths / 100.0;
        }

        return (measure - 1) * beatsPerMeasure + (beat - 1) + fraction;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                httpClient.Dispose();
                cts.Cancel();
                cts.Dispose();
                oscSocket.Dispose();
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}