using VUWare.Lib;

namespace VUWare.Tests;

public sealed class LinuxSerialDeviceDiscoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"vuware-{Guid.NewGuid():N}");

    [Fact]
    public void FindDevices_FiltersByUsbIdsAndPrefersStablePath()
    {
        if (!OperatingSystem.IsLinux()) return;

        var sys = Path.Combine(_root, "sys");
        var dev = Path.Combine(_root, "dev");
        Directory.CreateDirectory(Path.Combine(sys, "ttyUSB0"));
        Directory.CreateDirectory(Path.Combine(sys, "ttyUSB1"));
        Directory.CreateDirectory(Path.Combine(dev, "serial", "by-id"));
        File.WriteAllText(Path.Combine(sys, "ttyUSB0", "idVendor"), "0403\n");
        File.WriteAllText(Path.Combine(sys, "ttyUSB0", "idProduct"), "6015\n");
        File.WriteAllText(Path.Combine(sys, "ttyUSB1", "idVendor"), "1234\n");
        File.WriteAllText(Path.Combine(sys, "ttyUSB1", "idProduct"), "5678\n");
        File.WriteAllText(Path.Combine(dev, "ttyUSB0"), string.Empty);
        File.WriteAllText(Path.Combine(dev, "ttyUSB1"), string.Empty);
        var stablePath = Path.Combine(dev, "serial", "by-id", "usb-VU1");
        File.CreateSymbolicLink(stablePath, Path.Combine("..", "..", "ttyUSB0"));

        var devices = new LinuxSerialDeviceDiscovery(sys, dev).FindDevices();

        var device = Assert.Single(devices);
        Assert.Equal(Path.Combine(dev, "ttyUSB0"), device.DevicePath);
        Assert.Equal(stablePath, device.StablePath);
        Assert.Equal(stablePath, device.PreferredPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
