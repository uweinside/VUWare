namespace VUWare.Lib;

public sealed class LinuxSerialDeviceDiscovery : ISerialDeviceDiscovery
{
    public const string Vu1VendorId = "0403";
    public const string Vu1ProductId = "6015";

    private readonly string _sysTtyPath;
    private readonly string _devPath;

    public LinuxSerialDeviceDiscovery(string sysTtyPath = "/sys/class/tty", string devPath = "/dev")
    {
        _sysTtyPath = sysTtyPath;
        _devPath = devPath;
    }

    public IReadOnlyList<SerialDeviceInfo> FindDevices()
    {
        if (!OperatingSystem.IsLinux() || !Directory.Exists(_sysTtyPath)) return [];

        var stablePaths = FindStablePaths();
        var devices = new List<SerialDeviceInfo>();
        foreach (var ttyDirectory in Directory.EnumerateDirectories(_sysTtyPath))
        {
            var name = Path.GetFileName(ttyDirectory);
            if (!name.StartsWith("ttyUSB", StringComparison.Ordinal) &&
                !name.StartsWith("ttyACM", StringComparison.Ordinal)) continue;

            var ids = FindUsbIds(ttyDirectory);
            if (!string.Equals(ids.vendor, Vu1VendorId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(ids.product, Vu1ProductId, StringComparison.OrdinalIgnoreCase)) continue;

            var devicePath = Path.Combine(_devPath, name);
            stablePaths.TryGetValue(ResolvePath(devicePath), out var stablePath);
            devices.Add(new SerialDeviceInfo(
                devicePath,
                stablePath,
                ids.vendor,
                ids.product,
                CanOpen(devicePath, FileAccess.Read),
                CanOpen(devicePath, FileAccess.Write)));
        }

        return devices.OrderBy(device => device.PreferredPath, StringComparer.Ordinal).ToArray();
    }

    private Dictionary<string, string> FindStablePaths()
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var byId = Path.Combine(_devPath, "serial", "by-id");
        if (!Directory.Exists(byId)) return result;
        foreach (var path in Directory.EnumerateFiles(byId))
            result[ResolvePath(path)] = path;
        return result;
    }

    private static (string? vendor, string? product) FindUsbIds(string startPath)
    {
        var current = new DirectoryInfo(ResolvePath(startPath));
        for (var depth = 0; current is not null && depth < 10; depth++, current = current.Parent)
        {
            var vendorFile = Path.Combine(current.FullName, "idVendor");
            var productFile = Path.Combine(current.FullName, "idProduct");
            if (File.Exists(vendorFile) && File.Exists(productFile))
                return (File.ReadAllText(vendorFile).Trim(), File.ReadAllText(productFile).Trim());
        }
        return (null, null);
    }

    private static string ResolvePath(string path)
    {
        try
        {
            var target = File.ResolveLinkTarget(path, true) ?? Directory.ResolveLinkTarget(path, true);
            return target?.FullName ?? Path.GetFullPath(path);
        }
        catch (IOException) { return Path.GetFullPath(path); }
        catch (UnauthorizedAccessException) { return Path.GetFullPath(path); }
    }

    private static bool CanOpen(string path, FileAccess access)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, access, FileShare.ReadWrite);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return false; }
    }
}
