namespace VUWare.Lib;

public sealed class VU1Controller : IDisposable
{
    private readonly ISerialTransport _transport;
    private readonly DeviceManager _devices;
    private readonly bool _ownsTransport;
    private bool _initialized;

    public VU1Controller(int commandDelayMs = 50) : this(new SerialPortManager(), commandDelayMs, true) { }

    public VU1Controller(ISerialTransport transport, int commandDelayMs = 50) : this(transport, commandDelayMs, false) { }

    private VU1Controller(ISerialTransport transport, int commandDelayMs, bool ownsTransport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _devices = new DeviceManager(transport, commandDelayMs);
        _ownsTransport = ownsTransport;
    }

    public bool IsConnected => _transport.IsConnected;
    public bool IsInitialized => _initialized;
    public int DialCount => _devices.GetAllDials().Count;
    public string? PortName => _transport.PortName;

    public bool AutoDetectAndConnect()
    {
        Disconnect();
        return _transport is SerialPortManager manager && manager.AutoDetectAndConnect();
    }

    public bool Connect(string portName)
    {
        try { Disconnect(); _transport.Connect(portName); return true; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        { System.Diagnostics.Debug.WriteLine(exception); return false; }
    }

    public void Disconnect() { _transport.Disconnect(); _initialized = false; }

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        _initialized = await _devices.DiscoverDialsAsync(cancellationToken).ConfigureAwait(false);
        return _initialized;
    }

    public IReadOnlyDictionary<string, DialState> GetAllDials() => _devices.GetAllDials();
    public DialState? GetDial(string uid) => _devices.GetDialByUID(uid);
    public Task<bool> SetDialPercentageAsync(string uid, byte percentage, CancellationToken cancellationToken = default) =>
        _devices.SetDialPercentageAsync(uid, percentage, cancellationToken);
    public Task<bool> SetBacklightAsync(string uid, byte red, byte green, byte blue, byte white = 0, CancellationToken cancellationToken = default) =>
        _devices.SetBacklightAsync(uid, red, green, blue, white, cancellationToken);
    public Task<bool> SetBacklightColorAsync(string uid, NamedColor color, CancellationToken cancellationToken = default) =>
        SetBacklightAsync(uid, color.Red, color.Green, color.Blue, color.White, cancellationToken);
    public Task<bool> SetEasingConfigAsync(string uid, EasingConfig config, CancellationToken cancellationToken = default) =>
        _devices.SetEasingConfigAsync(uid, config, cancellationToken);

    public async Task<bool> SetDisplayImageAsync(string uid, byte[] imageData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageData);
        if (imageData.Length != ImageProcessor.BYTES_PER_IMAGE) throw new ArgumentException("Invalid packed image size.", nameof(imageData));
        var dial = GetDial(uid) ?? throw new KeyNotFoundException($"Dial '{uid}' was not discovered.");

        if (!await SendForSuccessAsync(CommandBuilder.DisplayClear(dial.Index), 1500, cancellationToken)) return false;
        if (!await SendForSuccessAsync(CommandBuilder.DisplayGotoXY(dial.Index, 0, 0), 1500, cancellationToken)) return false;
        foreach (var chunk in ImageProcessor.ChunkImageData(imageData))
        {
            if (!await SendForSuccessAsync(CommandBuilder.DisplayImageData(dial.Index, chunk), 2500, cancellationToken)) return false;
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        }
        await Task.Delay(200, cancellationToken).ConfigureAwait(false);
        return await SendForSuccessAsync(CommandBuilder.DisplayShowImage(dial.Index), 4000, cancellationToken);
    }

    private async Task<bool> SendForSuccessAsync(string command, int timeout, CancellationToken cancellationToken)
    {
        var response = await _transport.SendCommandAsync(command, timeout, cancellationToken).ConfigureAwait(false);
        return ProtocolHandler.IsSuccessResponse(ProtocolHandler.ParseResponse(response));
    }

    public void Dispose()
    {
        _devices.Dispose();
        if (_ownsTransport) _transport.Dispose();
        else Disconnect();
    }
}
