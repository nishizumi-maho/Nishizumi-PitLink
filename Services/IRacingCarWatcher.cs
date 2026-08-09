using System.IO.MemoryMappedFiles;
using System.Text;
using YamlDotNet.Serialization;

namespace NishizumiPitLink.Services;

public record CarInfo(string CarPath, string CarScreenName, string CarClassShortName, string TrackKey, string TrackDisplayName);

/// <summary>
/// Reads iRacing's live telemetry shared-memory block directly - the same memory-mapped file and
/// header layout iRacing documents in its public SDK (irsdk_defines.h) - so this app has no runtime
/// dependency on any third-party iRacing SDK wrapper. Only what this app actually needs is read:
/// the header fields that locate the session info YAML block, and just enough of that YAML (car and
/// track identity) to decide when to re-apply a MOZA Pit House preset. The full telemetry variable
/// buffer (car speed, inputs, etc.) is never touched.
/// </summary>
public class IRacingCarWatcher : IDisposable
{
    private const string MemoryMapName = "Local\\IRSDKMemMapFileName";
    private const int MemoryMapSize = 1164 * 1024;
    private const int StatusConnected = 0x0001;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1); // session info doesn't need per-tick polling
    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

    private readonly IDeserializer _yaml = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();

    private CancellationTokenSource? _cts;
    private Task? _pollTask;
    private int _lastSessionInfoUpdate = -1;
    private string? _lastCarPath;
    private string? _lastTrackKey;

    public event Action<bool>? ConnectionChanged;
    public event Action<CarInfo?>? CarChanged;
    public event Action<Exception>? SdkException;

    public bool IsConnected { get; private set; }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _pollTask = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _pollTask?.Wait(TimeSpan.FromSeconds(2)); } catch { /* best effort on shutdown */ }
        _cts?.Dispose();
        _cts = null;
        _pollTask = null;
    }

    private async Task PollLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                Poll();
            }
            catch (Exception ex)
            {
                SdkException?.Invoke(ex);
            }

            try { await Task.Delay(PollInterval, token); }
            catch (TaskCanceledException) { }
        }
    }

    private void Poll()
    {
        MemoryMappedFile mmf;
        try
        {
            mmf = MemoryMappedFile.OpenExisting(MemoryMapName, MemoryMappedFileRights.Read);
        }
        catch
        {
            SetConnected(false);
            return;
        }

        using (mmf)
        using (var view = mmf.CreateViewAccessor(0, MemoryMapSize, MemoryMappedFileAccess.Read))
        {
            var status = view.ReadInt32(4);
            if (!SetConnected((status & StatusConnected) != 0))
                return; // not connected - nothing further to read

            var sessionInfoUpdate = view.ReadInt32(12);
            if (sessionInfoUpdate == _lastSessionInfoUpdate)
                return; // session info hasn't changed since the last poll

            var sessionInfoLen = view.ReadInt32(16);
            var sessionInfoOffset = view.ReadInt32(20);
            if (sessionInfoLen <= 0 || sessionInfoOffset <= 0)
                return;

            var buffer = new byte[sessionInfoLen];
            view.ReadArray(sessionInfoOffset, buffer, 0, sessionInfoLen);

            // Only commit the "last seen" marker once the YAML actually parses, so a transient
            // malformed snapshot gets retried on the next poll instead of being silently skipped forever.
            if (TryParseAndRaise(buffer))
                _lastSessionInfoUpdate = sessionInfoUpdate;
        }
    }

    /// <returns>true if not connected (caller should stop), false if connected and the caller should keep going.</returns>
    private bool SetConnected(bool connected)
    {
        if (connected != IsConnected)
        {
            IsConnected = connected;
            ConnectionChanged?.Invoke(connected);
            if (!connected)
            {
                _lastCarPath = null;
                _lastTrackKey = null;
                _lastSessionInfoUpdate = -1;
                CarChanged?.Invoke(null);
            }
        }
        return !connected;
    }

    private bool TryParseAndRaise(byte[] rawSessionInfo)
    {
        try
        {
            var nullIndex = Array.IndexOf(rawSessionInfo, (byte)0);
            var length = nullIndex >= 0 ? nullIndex : rawSessionInfo.Length;

            var isUtf8 = length >= Utf8Bom.Length && rawSessionInfo.AsSpan(0, Utf8Bom.Length).SequenceEqual(Utf8Bom);
            var text = isUtf8
                ? Encoding.UTF8.GetString(rawSessionInfo, Utf8Bom.Length, length - Utf8Bom.Length)
                : Encoding.Latin1.GetString(rawSessionInfo, 0, length);
            text = StripNonPrintable(text);

            var session = _yaml.Deserialize<SessionInfoYaml>(text);
            var drivers = session?.DriverInfo?.Drivers;
            if (drivers is null)
                return true; // parsed fine, just nothing useful in it yet (e.g. very early in session load)

            var me = drivers.FirstOrDefault(d => d.CarIdx == session!.DriverInfo!.DriverCarIdx);
            if (me is null || string.IsNullOrEmpty(me.CarPath))
                return true;

            var trackKey = session!.WeekendInfo?.TrackName ?? string.Empty;
            var trackDisplayName = BuildTrackDisplayName(session.WeekendInfo?.TrackDisplayName, session.WeekendInfo?.TrackConfigName, trackKey);

            if (me.CarPath != _lastCarPath || trackKey != _lastTrackKey)
            {
                _lastCarPath = me.CarPath;
                _lastTrackKey = trackKey;
                CarChanged?.Invoke(new CarInfo(me.CarPath, me.CarScreenName ?? me.CarPath, me.CarClassShortName ?? string.Empty, trackKey, trackDisplayName));
            }

            return true;
        }
        catch (Exception ex)
        {
            SdkException?.Invoke(ex);
            return false;
        }
    }

    /// <summary>
    /// Drops characters outside YAML 1.1's allowed "printable" set (tab, LF, CR, U+0020-U+007E,
    /// U+0085, U+00A0-U+D7FF, U+E000-U+FFFD). iRacing's session info block can contain stray control
    /// bytes that would otherwise make YamlDotNet throw on an otherwise well-formed document.
    /// </summary>
    private static string StripNonPrintable(string input)
    {
        var result = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            var code = (int)c;
            var isAllowed =
                code == 0x09 || code == 0x0A || code == 0x0D ||
                (code >= 0x20 && code <= 0x7E) ||
                code == 0x85 ||
                (code >= 0xA0 && code <= 0xD7FF) ||
                (code >= 0xE000 && code <= 0xFFFD);
            if (isAllowed) result.Append(c);
        }
        return result.ToString();
    }

    private static string BuildTrackDisplayName(string? trackDisplayName, string? trackConfigName, string fallback)
    {
        var name = string.IsNullOrEmpty(trackDisplayName) ? fallback : trackDisplayName;
        return string.IsNullOrEmpty(trackConfigName) || trackConfigName == name
            ? name
            : $"{name} - {trackConfigName}";
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    // Minimal mirror of iRacing's session info YAML - only the fields this app actually needs.
    private class SessionInfoYaml
    {
        public WeekendInfoYaml? WeekendInfo { get; set; }
        public DriverInfoYaml? DriverInfo { get; set; }
    }

    private class WeekendInfoYaml
    {
        public string? TrackName { get; set; }
        public string? TrackDisplayName { get; set; }
        public string? TrackConfigName { get; set; }
    }

    private class DriverInfoYaml
    {
        public int DriverCarIdx { get; set; }
        public List<DriverYaml>? Drivers { get; set; }
    }

    private class DriverYaml
    {
        public int CarIdx { get; set; }
        public string? CarPath { get; set; }
        public string? CarScreenName { get; set; }
        public string? CarClassShortName { get; set; }
    }
}
