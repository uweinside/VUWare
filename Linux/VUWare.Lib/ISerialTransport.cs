namespace VUWare.Lib;

public interface ISerialTransport : IDisposable
{
    bool IsConnected { get; }
    string? PortName { get; }
    void Connect(string portName);
    void Disconnect();
    Task<string> SendCommandAsync(string command, int timeoutMs = 2000, CancellationToken cancellationToken = default);
}

public sealed record SerialDeviceInfo(
    string DevicePath,
    string? StablePath,
    string? VendorId,
    string? ProductId,
    bool CanRead,
    bool CanWrite)
{
    public string PreferredPath => StablePath ?? DevicePath;
    public bool IsAccessible => CanRead && CanWrite;
}

public interface ISerialDeviceDiscovery
{
    IReadOnlyList<SerialDeviceInfo> FindDevices();
}
