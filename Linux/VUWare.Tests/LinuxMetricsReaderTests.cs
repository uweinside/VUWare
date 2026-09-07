using VUWare.Lib.Sensors;
using VUWare.Sensors.Linux;

namespace VUWare.Tests;

public sealed class LinuxMetricsReaderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"vuware-sensors-{Guid.NewGuid():N}");
    private string ProcStat => Path.Combine(_root, "proc", "stat");
    private string HwmonRoot => Path.Combine(_root, "sys", "class", "hwmon");
    private string DrmRoot => Path.Combine(_root, "sys", "class", "drm");

    public LinuxMetricsReaderTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Read_CalculatesCpuLoadFromCounterDeltasWithoutDoubleCountingGuest()
    {
        Write(ProcStat, "cpu 100 0 100 800 0 0 0 0 500 200\n");
        var reader = CreateReader();

        var baseline = reader.Read();
        Assert.False(baseline.Diagnostics[LinuxMetricKeys.CpuLoad].IsAvailable);

        Write(ProcStat, "cpu 150 0 150 900 0 0 0 0 900 400\n");
        var reading = reader.Read().Values[LinuxMetricKeys.CpuLoad];

        Assert.Equal(50d, reading.Value, 6);
        Assert.Equal(SensorCategory.Load, reading.Category);
    }

    [Fact]
    public void Read_ResetsCpuBaselineWhenCountersRegress()
    {
        Write(ProcStat, "cpu 100 0 100 800 0 0 0 0\n");
        var reader = CreateReader();
        reader.Read();

        Write(ProcStat, "cpu 10 0 10 80 0 0 0 0\n");
        var reset = reader.Read();
        Assert.DoesNotContain(LinuxMetricKeys.CpuLoad, reset.Values.Keys);
        Assert.Contains("regressed", reset.Diagnostics[LinuxMetricKeys.CpuLoad].Message, StringComparison.OrdinalIgnoreCase);

        Write(ProcStat, "cpu 20 0 20 90 0 0 0 0\n");
        Assert.Equal(2d / 3d * 100d, reader.Read().Values[LinuxMetricKeys.CpuLoad].Value, 6);
    }

    [Fact]
    public void Read_PrefersTdieAndAmdGpuEdgeOnBootVgaDevice()
    {
        Write(ProcStat, "cpu 1 0 1 8 0 0 0 0\n");
        AddHwmon("hwmon8", "k10temp", ("temp1", "Tctl", "52000"), ("temp2", "Tdie", "47000"));
        AddGpu("card0", "0000:0e:00.0", bootVga: false, busy: "99", ("temp1", "edge", "80000"));
        AddGpu("card1", "0000:01:00.0", bootVga: true, busy: "37", ("temp1", "junction", "65000"), ("temp2", "edge", "59000"));

        var snapshot = CreateReader().Read();

        Assert.Equal(47d, snapshot.Values[LinuxMetricKeys.CpuTemperature].Value);
        Assert.Equal(37d, snapshot.Values[LinuxMetricKeys.GpuLoad].Value);
        Assert.Equal(59d, snapshot.Values[LinuxMetricKeys.GpuTemperature].Value);
        Assert.Contains("0000:01:00.0", snapshot.Values[LinuxMetricKeys.GpuLoad].StableIdentity);
    }

    [Fact]
    public void Read_IgnoresFaultedTemperaturesAndClampsGpuBusyValue()
    {
        Write(ProcStat, "cpu 1 0 1 8 0 0 0 0\n");
        var cpu = AddHwmon("hwmon2", "k10temp", ("temp1", "Tdie", "45000"), ("temp2", "Tctl", "50000"));
        Write(Path.Combine(cpu, "temp1_fault"), "1\n");
        AddGpu("card0", "0000:01:00.0", bootVga: true, busy: "140", ("temp1", "junction", "62000"));

        var snapshot = CreateReader().Read();

        Assert.Equal(50d, snapshot.Values[LinuxMetricKeys.CpuTemperature].Value);
        Assert.Equal(100d, snapshot.Values[LinuxMetricKeys.GpuLoad].Value);
        Assert.Equal(62d, snapshot.Values[LinuxMetricKeys.GpuTemperature].Value);
    }

    [Fact]
    public void Read_ReportsPartialAvailabilityWithoutSubstitutingZero()
    {
        Write(ProcStat, "cpu 1 0 1 8 0 0 0 0\n");
        AddHwmon("hwmon0", "k10temp", ("temp1", "Tdie", "bad"));

        var snapshot = CreateReader().Read();

        Assert.Empty(snapshot.Values);
        Assert.False(snapshot.Diagnostics[LinuxMetricKeys.CpuTemperature].IsAvailable);
        Assert.False(snapshot.Diagnostics[LinuxMetricKeys.GpuLoad].IsAvailable);
        Assert.Contains("No AMD GPU", snapshot.Diagnostics[LinuxMetricKeys.GpuLoad].Message);
    }

    private LinuxMetricsReader CreateReader() => new(ProcStat, HwmonRoot, DrmRoot);

    private string AddHwmon(string directoryName, string name, params (string Stem, string Label, string Value)[] channels)
    {
        var directory = Path.Combine(HwmonRoot, directoryName);
        Write(Path.Combine(directory, "name"), name + "\n");
        foreach (var channel in channels)
        {
            Write(Path.Combine(directory, $"{channel.Stem}_label"), channel.Label + "\n");
            Write(Path.Combine(directory, $"{channel.Stem}_input"), channel.Value + "\n");
        }
        return directory;
    }

    private void AddGpu(
        string cardName,
        string pciAddress,
        bool bootVga,
        string busy,
        params (string Stem, string Label, string Value)[] channels)
    {
        var device = Path.Combine(DrmRoot, cardName, "device");
        Write(Path.Combine(device, "vendor"), "0x1002\n");
        Write(Path.Combine(device, "boot_vga"), bootVga ? "1\n" : "0\n");
        Write(Path.Combine(device, "gpu_busy_percent"), busy + "\n");
        Write(Path.Combine(device, "uevent"), $"DRIVER=amdgpu\nPCI_SLOT_NAME={pciAddress}\n");
        var hwmon = Path.Combine(device, "hwmon", "hwmon9");
        Write(Path.Combine(hwmon, "name"), "amdgpu\n");
        foreach (var channel in channels)
        {
            Write(Path.Combine(hwmon, $"{channel.Stem}_label"), channel.Label + "\n");
            Write(Path.Combine(hwmon, $"{channel.Stem}_input"), channel.Value + "\n");
        }
    }

    private static void Write(string path, string value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, value);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }
}
