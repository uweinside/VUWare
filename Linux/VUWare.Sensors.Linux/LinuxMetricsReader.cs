// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.RegularExpressions;
using VUWare.Lib.Sensors;

namespace VUWare.Sensors.Linux;

public static class LinuxMetricKeys
{
    public const string CpuLoad = "cpu-load";
    public const string CpuTemperature = "cpu-temp";
    public const string GpuLoad = "gpu-load";
    public const string GpuTemperature = "gpu-temp";

    public static IReadOnlyList<string> All { get; } =
        [CpuLoad, CpuTemperature, GpuLoad, GpuTemperature];
}

public sealed record LinuxMetricValue(
    string Key,
    string SensorName,
    string EntryName,
    SensorCategory Category,
    double Value,
    string Unit,
    string Source,
    string StableIdentity);

public sealed record LinuxMetricDiagnostic(string Key, bool IsAvailable, string Message, string? Source = null);

public sealed record LinuxMetricsSnapshot(
    DateTime Timestamp,
    IReadOnlyDictionary<string, LinuxMetricValue> Values,
    IReadOnlyDictionary<string, LinuxMetricDiagnostic> Diagnostics);

/// <summary>Reads the Linux kernel ABIs used by the AMD prototype.</summary>
public sealed partial class LinuxMetricsReader
{
    private readonly string _procStatPath;
    private readonly string _hwmonRoot;
    private readonly string _drmRoot;
    private CpuTimes? _previousCpuTimes;
    private GpuDevice? _gpu;

    public LinuxMetricsReader(
        string procStatPath = "/proc/stat",
        string hwmonRoot = "/sys/class/hwmon",
        string drmRoot = "/sys/class/drm")
    {
        _procStatPath = procStatPath;
        _hwmonRoot = hwmonRoot;
        _drmRoot = drmRoot;
    }

    public LinuxMetricsSnapshot Read()
    {
        var values = new Dictionary<string, LinuxMetricValue>(StringComparer.OrdinalIgnoreCase);
        var diagnostics = LinuxMetricKeys.All.ToDictionary(
            key => key,
            key => new LinuxMetricDiagnostic(key, false, "Not sampled."),
            StringComparer.OrdinalIgnoreCase);

        ReadCpuLoad(values, diagnostics);
        ReadCpuTemperature(values, diagnostics);
        ReadGpu(values, diagnostics);

        return new LinuxMetricsSnapshot(DateTime.UtcNow, values, diagnostics);
    }

    private void ReadCpuLoad(
        IDictionary<string, LinuxMetricValue> values,
        IDictionary<string, LinuxMetricDiagnostic> diagnostics)
    {
        if (!TryReadCpuTimes(out var current, out var error))
        {
            diagnostics[LinuxMetricKeys.CpuLoad] = new(LinuxMetricKeys.CpuLoad, false, error, _procStatPath);
            return;
        }

        var previous = _previousCpuTimes;
        _previousCpuTimes = current;
        if (previous is null)
        {
            diagnostics[LinuxMetricKeys.CpuLoad] = new(
                LinuxMetricKeys.CpuLoad, false, "Waiting for a second /proc/stat sample.", _procStatPath);
            return;
        }

        if (current.Total < previous.Value.Total || current.Idle < previous.Value.Idle)
        {
            diagnostics[LinuxMetricKeys.CpuLoad] = new(
                LinuxMetricKeys.CpuLoad, false, "CPU counters regressed; sampling baseline was reset.", _procStatPath);
            return;
        }

        var totalDelta = current.Total - previous.Value.Total;
        var idleDelta = current.Idle - previous.Value.Idle;
        if (totalDelta == 0 || idleDelta > totalDelta)
        {
            diagnostics[LinuxMetricKeys.CpuLoad] = new(
                LinuxMetricKeys.CpuLoad, false, "CPU counters did not produce a valid sampling interval.", _procStatPath);
            return;
        }

        var load = Math.Clamp(100d * (totalDelta - idleDelta) / totalDelta, 0d, 100d);
        AddValue(values, diagnostics, new(
            LinuxMetricKeys.CpuLoad, "CPU", "Load", SensorCategory.Load,
            load, "%", _procStatPath, "cpu:aggregate"));
    }

    private bool TryReadCpuTimes(out CpuTimes times, out string error)
    {
        times = default;
        try
        {
            var line = File.ReadLines(_procStatPath)
                .FirstOrDefault(value => value.StartsWith("cpu ", StringComparison.Ordinal));
            if (line is null)
            {
                error = "Aggregate CPU counters were not found.";
                return false;
            }

            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (fields.Length < 9)
            {
                error = "Aggregate CPU counter line has too few fields.";
                return false;
            }

            var counters = new ulong[8];
            for (var index = 0; index < counters.Length; index++)
            {
                if (!ulong.TryParse(fields[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out counters[index]))
                {
                    error = "Aggregate CPU counter line contains an invalid number.";
                    return false;
                }
            }

            times = new CpuTimes(SaturatingSum(counters), SaturatingSum([counters[3], counters[4]]));
            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error = $"Cannot read CPU counters: {exception.Message}";
            return false;
        }
    }

    private void ReadCpuTemperature(
        IDictionary<string, LinuxMetricValue> values,
        IDictionary<string, LinuxMetricDiagnostic> diagnostics)
    {
        var devices = GetDirectories(_hwmonRoot, "hwmon*")
            .Where(path => ReadText(Path.Combine(path, "name"))?.Equals("k10temp", StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(path => CanonicalPath(path), StringComparer.Ordinal)
            .ToList();

        var channels = devices.SelectMany(ReadTemperatureChannels).ToList();
        var selected = channels.FirstOrDefault(channel => channel.Label.Equals("Tdie", StringComparison.OrdinalIgnoreCase))
            ?? channels.FirstOrDefault(channel => channel.Label.Equals("Tctl", StringComparison.OrdinalIgnoreCase))
            ?? channels.OrderByDescending(channel => channel.Value).FirstOrDefault();

        if (selected is null)
        {
            diagnostics[LinuxMetricKeys.CpuTemperature] = new(
                LinuxMetricKeys.CpuTemperature, false,
                "No readable k10temp temperature channel was found.", _hwmonRoot);
            return;
        }

        AddValue(values, diagnostics, new(
            LinuxMetricKeys.CpuTemperature, "CPU", "Temperature", SensorCategory.Temperature,
            selected.Value, "°C", selected.InputPath, $"cpu:k10temp:{selected.Label}"));
    }

    private void ReadGpu(
        IDictionary<string, LinuxMetricValue> values,
        IDictionary<string, LinuxMetricDiagnostic> diagnostics)
    {
        _gpu ??= DiscoverPrimaryAmdGpu();
        if (_gpu is null)
        {
            diagnostics[LinuxMetricKeys.GpuLoad] = new(LinuxMetricKeys.GpuLoad, false, "No AMD GPU using amdgpu was found.", _drmRoot);
            diagnostics[LinuxMetricKeys.GpuTemperature] = new(LinuxMetricKeys.GpuTemperature, false, "No AMD GPU using amdgpu was found.", _drmRoot);
            return;
        }

        var busyPath = Path.Combine(_gpu.DevicePath, "gpu_busy_percent");
        if (TryReadDouble(busyPath, out var busy))
        {
            AddValue(values, diagnostics, new(
                LinuxMetricKeys.GpuLoad, "AMD GPU", "Load", SensorCategory.Load,
                Math.Clamp(busy, 0d, 100d), "%", busyPath, $"gpu:{_gpu.PciAddress}"));
        }
        else
        {
            diagnostics[LinuxMetricKeys.GpuLoad] = new(
                LinuxMetricKeys.GpuLoad, false, "The amdgpu busy percentage is unavailable or malformed.", busyPath);
        }

        var hwmonDirectories = GetDirectories(Path.Combine(_gpu.DevicePath, "hwmon"), "hwmon*");
        if (hwmonDirectories.Count == 0)
        {
            hwmonDirectories = GetDirectories(_hwmonRoot, "hwmon*")
                .Where(path => ReadText(Path.Combine(path, "name"))?.Equals("amdgpu", StringComparison.OrdinalIgnoreCase) == true)
                .Where(path => CanonicalPath(path).Contains(_gpu.PciAddress, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var channels = hwmonDirectories.SelectMany(ReadTemperatureChannels).ToList();
        var selected = channels.FirstOrDefault(channel => channel.Label.Equals("edge", StringComparison.OrdinalIgnoreCase))
            ?? channels.FirstOrDefault(channel => channel.Label.Equals("junction", StringComparison.OrdinalIgnoreCase))
            ?? channels.FirstOrDefault();

        if (selected is not null)
        {
            AddValue(values, diagnostics, new(
                LinuxMetricKeys.GpuTemperature, "AMD GPU", "Temperature", SensorCategory.Temperature,
                selected.Value, "°C", selected.InputPath, $"gpu:{_gpu.PciAddress}:{selected.Label}"));
        }
        else
        {
            diagnostics[LinuxMetricKeys.GpuTemperature] = new(
                LinuxMetricKeys.GpuTemperature, false, "No readable amdgpu temperature channel was found.", _gpu.DevicePath);
        }

        if (!Directory.Exists(_gpu.DevicePath))
            _gpu = null;
    }

    private GpuDevice? DiscoverPrimaryAmdGpu()
    {
        return GetDirectories(_drmRoot, "card*")
            .Where(path => CardNameRegex().IsMatch(Path.GetFileName(path)))
            .Select(path => Path.Combine(path, "device"))
            .Where(Directory.Exists)
            .Where(path => ReadText(Path.Combine(path, "vendor"))?.Equals("0x1002", StringComparison.OrdinalIgnoreCase) == true)
            .Where(IsAmdGpuDriver)
            .Select(path => new GpuDevice(path, GetPciAddress(path), ReadText(Path.Combine(path, "boot_vga")) == "1"))
            .OrderByDescending(device => device.IsBootVga)
            .ThenBy(device => device.PciAddress, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool IsAmdGpuDriver(string devicePath)
    {
        var driverPath = Path.Combine(devicePath, "driver");
        if (Directory.Exists(driverPath))
            return Path.GetFileName(CanonicalPath(driverPath)).Equals("amdgpu", StringComparison.OrdinalIgnoreCase);

        return File.Exists(Path.Combine(devicePath, "gpu_busy_percent"));
    }

    private static string GetPciAddress(string devicePath)
    {
        var canonical = CanonicalPath(devicePath);
        var match = PciAddressRegex().Matches(canonical).LastOrDefault();
        if (match is not null)
            return match.Value.ToLowerInvariant();

        var uevent = ReadText(Path.Combine(devicePath, "uevent"));
        var slot = uevent?.Split('\n').FirstOrDefault(line => line.StartsWith("PCI_SLOT_NAME=", StringComparison.Ordinal));
        return slot is null
            ? Path.GetFileName(Path.GetDirectoryName(devicePath)) ?? "unknown"
            : slot["PCI_SLOT_NAME=".Length..].ToLowerInvariant();
    }

    private static IEnumerable<TemperatureChannel> ReadTemperatureChannels(string hwmonPath)
    {
        foreach (var inputPath in GetFiles(hwmonPath, "temp*_input").OrderBy(path => path, StringComparer.Ordinal))
        {
            var stem = Path.GetFileName(inputPath)[..^"_input".Length];
            if (ReadText(Path.Combine(hwmonPath, $"{stem}_enable")) == "0" ||
                ReadText(Path.Combine(hwmonPath, $"{stem}_fault")) == "1" ||
                !TryReadDouble(inputPath, out var millidegrees))
                continue;

            var value = millidegrees / 1000d;
            if (value is < -100d or > 250d)
                continue;

            var label = ReadText(Path.Combine(hwmonPath, $"{stem}_label")) ?? stem;
            yield return new TemperatureChannel(label, value, inputPath);
        }
    }

    private static void AddValue(
        IDictionary<string, LinuxMetricValue> values,
        IDictionary<string, LinuxMetricDiagnostic> diagnostics,
        LinuxMetricValue value)
    {
        values[value.Key] = value;
        diagnostics[value.Key] = new(value.Key, true, $"Reading from {value.Source}.", value.Source);
    }

    private static string? ReadText(string path)
    {
        try { return File.ReadAllText(path).Trim(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return null; }
    }

    private static bool TryReadDouble(string path, out double value) =>
        double.TryParse(ReadText(path), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    private static List<string> GetDirectories(string path, string pattern)
    {
        try { return Directory.Exists(path) ? Directory.GetDirectories(path, pattern).ToList() : []; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return []; }
    }

    private static List<string> GetFiles(string path, string pattern)
    {
        try { return Directory.Exists(path) ? Directory.GetFiles(path, pattern).ToList() : []; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return []; }
    }

    private static string CanonicalPath(string path)
    {
        try { return Path.GetFullPath(new DirectoryInfo(path).ResolveLinkTarget(true)?.FullName ?? path); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return Path.GetFullPath(path); }
    }

    private static ulong SaturatingSum(IEnumerable<ulong> values)
    {
        ulong result = 0;
        foreach (var value in values)
            result = ulong.MaxValue - result < value ? ulong.MaxValue : result + value;
        return result;
    }

    private readonly record struct CpuTimes(ulong Total, ulong Idle);
    private sealed record GpuDevice(string DevicePath, string PciAddress, bool IsBootVga);
    private sealed record TemperatureChannel(string Label, double Value, string InputPath);

    [GeneratedRegex("^card[0-9]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CardNameRegex();

    [GeneratedRegex("[0-9a-fA-F]{4}:[0-9a-fA-F]{2}:[0-9a-fA-F]{2}\\.[0-7]", RegexOptions.CultureInvariant)]
    private static partial Regex PciAddressRegex();
}
