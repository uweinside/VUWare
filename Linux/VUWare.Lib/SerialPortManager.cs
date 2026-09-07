using System.IO.Ports;
using System.Text;

namespace VUWare.Lib;

public sealed class SerialPortManager : ISerialTransport
{
    private const int BaudRate = 115200;
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private readonly ISerialDeviceDiscovery _discovery;
    private SerialPort? _port;
    private CancellationTokenSource _connectionLifetime = new();
    private bool _disposed;

    public SerialPortManager(ISerialDeviceDiscovery? discovery = null) =>
        _discovery = discovery ?? new LinuxSerialDeviceDiscovery();

    public bool IsConnected => _port?.IsOpen == true;
    public string? PortName => _port?.PortName;

    public bool AutoDetectAndConnect()
    {
        foreach (var device in _discovery.FindDevices())
        {
            try
            {
                Connect(device.PreferredPath);
                var response = SendCommandAsync(CommandBuilder.RescanBus(), 2000).GetAwaiter().GetResult();
                if (response.StartsWith('<') && ProtocolHandler.ParseResponse(response).Command == CommandBuilder.COMM_CMD_RESCAN_BUS)
                    return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TimeoutException or InvalidOperationException or FormatException)
            {
                Disconnect();
            }
        }
        return false;
    }

    public void Connect(string portName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);
        Disconnect();

        var port = new SerialPort(portName, BaudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            NewLine = "\r\n",
            ReadTimeout = 2000,
            WriteTimeout = 2000,
            DtrEnable = true,
            RtsEnable = true
        };

        try
        {
            port.Open();
            _connectionLifetime.Dispose();
            _connectionLifetime = new CancellationTokenSource();
            _port = port;
        }
        catch
        {
            port.Dispose();
            throw;
        }
    }

    public void Disconnect()
    {
        _connectionLifetime.Cancel();
        var port = Interlocked.Exchange(ref _port, null);
        if (port is null) return;
        try { port.Close(); }
        finally { port.Dispose(); }
    }

    public async Task<string> SendCommandAsync(string command, int timeoutMs = 2000, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (timeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMs));

        using var timeout = new CancellationTokenSource(timeoutMs);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token, _connectionLifetime.Token);
        var lockTaken = false;
        try
        {
            await _commandLock.WaitAsync(linked.Token).ConfigureAwait(false);
            lockTaken = true;
            var port = _port;
            if (port?.IsOpen != true) throw new InvalidOperationException("The serial port is not connected.");

            port.DiscardInBuffer();
            var bytes = Encoding.ASCII.GetBytes(command + "\r\n");
            await port.BaseStream.WriteAsync(bytes, linked.Token).ConfigureAwait(false);
            await port.BaseStream.FlushAsync(linked.Token).ConfigureAwait(false);
            return await ReadResponseAsync(port.BaseStream, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"No complete response was received within {timeoutMs} ms.", exception);
        }
        finally
        {
            if (lockTaken) _commandLock.Release();
        }
    }

    private static async Task<string> ReadResponseAsync(Stream stream, CancellationToken cancellationToken)
    {
        var response = new StringBuilder();
        var buffer = new byte[256];
        var expectedCharacters = -1;

        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException("The serial device closed the connection.");

            for (var i = 0; i < read; i++)
            {
                var character = (char)buffer[i];
                if (character == '<')
                {
                    response.Clear();
                    expectedCharacters = -1;
                }
                if (response.Length == 0 && character != '<') continue;
                if (character is '\r' or '\n') continue;
                response.Append(character);

                if (response.Length == 9)
                {
                    if (!ushort.TryParse(response.ToString(5, 4), System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out var dataLength))
                        throw new FormatException("The response contains an invalid length field.");
                    expectedCharacters = 9 + dataLength * 2;
                }

                if (expectedCharacters >= 9 && response.Length == expectedCharacters)
                    return response.ToString();
                if (response.Length > 131_081)
                    throw new InvalidDataException("The response exceeds the protocol maximum.");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        Disconnect();
        _connectionLifetime.Dispose();
        _commandLock.Dispose();
        _disposed = true;
    }
}
