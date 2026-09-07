using VUWare.Sensors.Linux;

namespace VUWare.Tests;

public sealed class LinuxSensorProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"vuware-provider-{Guid.NewGuid():N}");
    private string ProcStat => Path.Combine(_root, "proc", "stat");
    private string HwmonRoot => Path.Combine(_root, "hwmon");
    private string DrmRoot => Path.Combine(_root, "drm");

    [Fact]
    public void Provider_RejectsMissingKernelSources()
    {
        using var provider = new LinuxSensorProvider(
            new LinuxMetricsReader(ProcStat, HwmonRoot, DrmRoot),
            TimeSpan.FromMilliseconds(25));

        Assert.False(provider.Connect());
        Assert.False(provider.IsConnected);
    }

    [Fact]
    public async Task Provider_SamplesSnapshotsTracksStatisticsAndStopsCleanly()
    {
        Write(ProcStat, "cpu 100 0 100 800 0 0 0 0\n");
        var cpu = Path.Combine(HwmonRoot, "hwmon7");
        Write(Path.Combine(cpu, "name"), "k10temp\n");
        Write(Path.Combine(cpu, "temp1_label"), "Tdie\n");
        Write(Path.Combine(cpu, "temp1_input"), "40000\n");

        using var provider = new LinuxSensorProvider(
            new LinuxMetricsReader(ProcStat, HwmonRoot, DrmRoot),
            TimeSpan.FromMilliseconds(25));

        Assert.True(provider.Connect());
        Write(ProcStat, "cpu 150 0 150 900 0 0 0 0\n");
        Write(Path.Combine(cpu, "temp1_input"), "50000\n");
        Assert.True(await provider.WaitForFirstSampleAsync(TimeSpan.FromSeconds(2)));

        var load = provider.GetMetric(LinuxMetricKeys.CpuLoad);
        var temperature = provider.GetMetric(LinuxMetricKeys.CpuTemperature);
        Assert.NotNull(load);
        Assert.Equal(50d, load.Value, 6);
        Assert.NotNull(temperature);
        Assert.Equal(40d, temperature.ValueMin);
        Assert.Equal(50d, temperature.ValueMax);
        Assert.Equal(45d, temperature.ValueAvg);
        Assert.True(provider.IsConnected);

        provider.Disconnect();
        Assert.False(provider.IsConnected);
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
