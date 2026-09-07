using System.Globalization;

namespace VUWare.Lib;

public sealed class DeviceManager : IDisposable
{
    private readonly ISerialTransport _transport;
    private readonly Dictionary<string, DialState> _dials = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<byte, string> _indexToUid = [];
    private readonly object _stateLock = new();
    private readonly int _commandDelayMs;

    public DeviceManager(ISerialTransport transport, int commandDelayMs = 50)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _commandDelayMs = Math.Clamp(commandDelayMs, 10, 500);
    }

    public IReadOnlyDictionary<string, DialState> GetAllDials()
    {
        lock (_stateLock) return new Dictionary<string, DialState>(_dials, StringComparer.OrdinalIgnoreCase);
    }

    public DialState? GetDialByUID(string uid)
    {
        if (!TryNormalizeUid(uid, out var normalizedUid)) return null;
        lock (_stateLock) return _dials.GetValueOrDefault(normalizedUid);
    }

    public DialState? GetDialByIndex(byte index)
    {
        lock (_stateLock) return _indexToUid.TryGetValue(index, out var uid) ? _dials.GetValueOrDefault(uid) : null;
    }

    public async Task<bool> DiscoverDialsAsync(CancellationToken cancellationToken = default)
    {
        if (!_transport.IsConnected) throw new InvalidOperationException("The VU1 hub is not connected.");
        try
        {
            if (!await SendForSuccessAsync(CommandBuilder.RescanBus(), 3000, cancellationToken)) return false;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                await TryProvisionAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            var map = await ReadDeviceMapAsync(cancellationToken).ConfigureAwait(false);
            lock (_stateLock) _indexToUid.Clear();
            for (var index = 0; index < Math.Min(map.Length, 100); index++)
            {
                if (map[index] == 1) await RegisterDialAsync((byte)index, cancellationToken).ConfigureAwait(false);
            }
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"Dial discovery failed: {exception.Message}");
            return false;
        }
    }

    private async Task TryProvisionAsync(CancellationToken cancellationToken)
    {
        try { await SendForSuccessAsync(CommandBuilder.ProvisionDevice(), 3000, cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        { System.Diagnostics.Debug.WriteLine($"Provision attempt failed: {exception.Message}"); }
    }

    private async Task<byte[]> ReadDeviceMapAsync(CancellationToken cancellationToken)
    {
        var response = await _transport.SendCommandAsync(CommandBuilder.GetDevicesMap(), 5000, cancellationToken).ConfigureAwait(false);
        var message = ProtocolHandler.ParseResponse(response);
        if (!ProtocolHandler.IsSuccessResponse(message)) throw new InvalidOperationException($"Device-map request failed: {ProtocolHandler.GetStatusCode(message)}");
        return message.BinaryData;
    }

    private async Task RegisterDialAsync(byte index, CancellationToken cancellationToken)
    {
        var response = await _transport.SendCommandAsync(CommandBuilder.GetDeviceUID(index), 2000, cancellationToken).ConfigureAwait(false);
        var message = ProtocolHandler.ParseResponse(response);
        if (!ProtocolHandler.IsSuccessResponse(message) || message.BinaryData.Length == 0) return;
        if (!TryNormalizeUid(message.RawData, out var uid)) return;
        lock (_stateLock)
        {
            if (!_dials.TryGetValue(uid, out var dial))
            {
                dial = new DialState { UID = uid, Name = $"Dial_{uid[..Math.Min(uid.Length, 8)]}" };
                _dials.Add(uid, dial);
            }
            dial.Index = index;
            dial.LastCommunication = DateTime.UtcNow;
            _indexToUid[index] = uid;
        }
    }

    public async Task<bool> SetDialPercentageAsync(string uid, byte percentage, CancellationToken cancellationToken = default)
    {
        if (percentage > 100) throw new ArgumentOutOfRangeException(nameof(percentage));
        var dial = RequireDial(uid);
        if (!await SendForSuccessAsync(CommandBuilder.SetDialPercentage(dial.Index, percentage), 5000, cancellationToken)) return false;
        lock (_stateLock) { dial.CurrentValue = percentage; dial.ValuePending = false; dial.LastCommunication = DateTime.UtcNow; }
        return true;
    }

    public async Task<bool> SetBacklightAsync(string uid, byte red, byte green, byte blue, byte white = 0, CancellationToken cancellationToken = default)
    {
        var dial = RequireDial(uid);
        if (!await SendForSuccessAsync(CommandBuilder.SetRGBBacklight(dial.Index, red, green, blue, white), 5000, cancellationToken)) return false;
        lock (_stateLock) { dial.Backlight = new(red, green, blue, white); dial.BacklightPending = false; dial.LastCommunication = DateTime.UtcNow; }
        return true;
    }

    public async Task<bool> SetEasingConfigAsync(string uid, EasingConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        var dial = RequireDial(uid);
        var commands = new[]
        {
            CommandBuilder.SetDialEasingStep(dial.Index, config.DialStep),
            CommandBuilder.SetDialEasingPeriod(dial.Index, config.DialPeriod),
            CommandBuilder.SetBacklightEasingStep(dial.Index, config.BacklightStep),
            CommandBuilder.SetBacklightEasingPeriod(dial.Index, config.BacklightPeriod)
        };
        foreach (var command in commands)
        {
            if (!await SendForSuccessAsync(command, 5000, cancellationToken)) return false;
            await Task.Delay(_commandDelayMs, cancellationToken).ConfigureAwait(false);
        }
        lock (_stateLock) { dial.Easing = config; dial.LastCommunication = DateTime.UtcNow; }
        return true;
    }

    private DialState RequireDial(string uid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uid);
        return GetDialByUID(uid) ?? throw new KeyNotFoundException($"Dial '{uid}' was not discovered.");
    }

    internal static bool TryNormalizeUid(string? uid, out string normalizedUid)
    {
        normalizedUid = string.Empty;
        if (string.IsNullOrWhiteSpace(uid)) return false;

        normalizedUid = string.Concat(uid.Where(character =>
            !char.IsWhiteSpace(character) &&
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.Format));

        return normalizedUid.Length > 0 && normalizedUid.All(Uri.IsHexDigit);
    }

    private async Task<bool> SendForSuccessAsync(string command, int timeout, CancellationToken cancellationToken)
    {
        var response = await _transport.SendCommandAsync(command, timeout, cancellationToken).ConfigureAwait(false);
        return ProtocolHandler.IsSuccessResponse(ProtocolHandler.ParseResponse(response));
    }

    public void Dispose() { }
}
