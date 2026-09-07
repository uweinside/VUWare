// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using VUWare.Lib.Sensors;

namespace VUWare.Sensors.Linux;

/// <summary>Samples CPU and AMD GPU metrics from procfs and sysfs.</summary>
public sealed class LinuxSensorProvider : ISensorProvider
{
    private readonly object _sync = new();
    private readonly LinuxMetricsReader _reader;
    private readonly TimeSpan _samplingInterval;
    private readonly Dictionary<string, RunningStatistics> _statistics = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<ISensorReading> _readings = [];
    private IReadOnlyDictionary<string, LinuxMetricDiagnostic> _diagnostics =
        new Dictionary<string, LinuxMetricDiagnostic>(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _samplingCancellation;
    private Task? _samplingTask;
    private TaskCompletionSource _firstSample = NewCompletionSource();
    private bool _disposed;

    public LinuxSensorProvider(
        LinuxMetricsReader? reader = null,
        TimeSpan? samplingInterval = null)
    {
        _reader = reader ?? new LinuxMetricsReader();
        _samplingInterval = samplingInterval ?? TimeSpan.FromSeconds(1);
        if (_samplingInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(samplingInterval));
    }

    public string ProviderName => "Linux procfs/sysfs";

    public bool IsConnected { get; private set; }

    public IReadOnlyDictionary<string, LinuxMetricDiagnostic> Diagnostics
    {
        get { lock (_sync) return _diagnostics; }
    }

    public event EventHandler<SensorProviderConnectionEventArgs>? ConnectionStatusChanged;

    public bool Connect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsConnected)
            return true;
        if (!OperatingSystem.IsLinux())
            return false;

        LinuxMetricsSnapshot initial;
        try { initial = _reader.Read(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ConnectionStatusChanged?.Invoke(this, new(false, $"Linux sensor initialization failed: {exception.Message}"));
            return false;
        }

        var hasReadableSource = initial.Values.Count > 0 ||
            initial.Diagnostics.TryGetValue(LinuxMetricKeys.CpuLoad, out var cpuDiagnostic) &&
            cpuDiagnostic.Message.StartsWith("Waiting for a second", StringComparison.Ordinal);
        if (!hasReadableSource)
            return false;

        lock (_sync)
        {
            _firstSample = NewCompletionSource();
            Publish(initial);
            _samplingCancellation = new CancellationTokenSource();
            IsConnected = true;
            _samplingTask = Task.Run(() => SamplingLoopAsync(_samplingCancellation.Token));
        }

        ConnectionStatusChanged?.Invoke(this, new(true, "Sampling Linux procfs/sysfs sensors."));
        return true;
    }

    public void Disconnect()
    {
        CancellationTokenSource? cancellation;
        Task? task;
        lock (_sync)
        {
            if (!IsConnected)
                return;
            IsConnected = false;
            cancellation = _samplingCancellation;
            task = _samplingTask;
            _samplingCancellation = null;
            _samplingTask = null;
        }

        cancellation?.Cancel();
        try { task?.Wait(TimeSpan.FromSeconds(2)); }
        catch (AggregateException exception) when (exception.InnerExceptions.All(item => item is OperationCanceledException)) { }
        cancellation?.Dispose();
        ConnectionStatusChanged?.Invoke(this, new(false, "Linux sensor sampling stopped."));
    }

    public async Task<bool> WaitForFirstSampleAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            return false;

        try
        {
            await _firstSample.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException) { return false; }
    }

    public ISensorReading? GetMetric(string key)
    {
        lock (_sync)
            return _readings.FirstOrDefault(reading => reading.EntryName.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<ISensorDescriptor> GetSensors()
    {
        lock (_sync)
        {
            return _readings
                .GroupBy(reading => new { reading.SensorId, reading.SensorInstance, reading.SensorName })
                .Select(group => (ISensorDescriptor)new LinuxSensorDescriptor(
                    group.Key.SensorId, group.Key.SensorInstance, group.Key.SensorName, group.Key.SensorName))
                .ToArray();
        }
    }

    public IReadOnlyList<ISensorReading> GetAllReadings()
    {
        lock (_sync) return _readings;
    }

    public IReadOnlyList<ISensorReading> GetReadingsByType(SensorCategory type)
    {
        lock (_sync) return _readings.Where(reading => reading.Category == type).ToArray();
    }

    public ISensorReading? GetReading(string sensorName, string entryName)
    {
        lock (_sync)
        {
            return _readings.FirstOrDefault(reading =>
                reading.SensorName.Equals(sensorName, StringComparison.OrdinalIgnoreCase) &&
                reading.EntryName.Equals(entryName, StringComparison.OrdinalIgnoreCase));
        }
    }

    public IReadOnlyList<SensorCategory> GetAvailableCategories()
    {
        lock (_sync) return _readings.Select(reading => reading.Category).Distinct().Order().ToArray();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        Disconnect();
        _disposed = true;
    }

    private async Task SamplingLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_samplingInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                var snapshot = _reader.Read();
                lock (_sync) Publish(snapshot);
                _firstSample.TrySetResult();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _firstSample.TrySetException(exception);
            lock (_sync) IsConnected = false;
            ConnectionStatusChanged?.Invoke(this, new(false, $"Linux sensor sampling stopped: {exception.Message}"));
        }
    }

    private void Publish(LinuxMetricsSnapshot snapshot)
    {
        var readings = new List<ISensorReading>(snapshot.Values.Count);
        foreach (var value in snapshot.Values.Values.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            if (!_statistics.TryGetValue(value.Key, out var statistics))
            {
                statistics = new RunningStatistics();
                _statistics[value.Key] = statistics;
            }

            statistics.Add(value.Value);
            var sensorId = StableId(value.StableIdentity.Split(':')[..2].Aggregate((left, right) => $"{left}:{right}"));
            readings.Add(new LinuxSensorReading(
                sensorId,
                0,
                value.SensorName,
                StableId(value.Key),
                value.Key,
                value.Category,
                value.Value,
                statistics.Minimum,
                statistics.Maximum,
                statistics.Average,
                value.Unit,
                snapshot.Timestamp,
                value.Source));
        }

        _readings = readings;
        _diagnostics = snapshot.Diagnostics;
    }

    private static uint StableId(string value)
    {
        const uint offset = 2166136261;
        const uint prime = 16777619;
        var hash = offset;
        foreach (var character in value)
        {
            hash ^= character;
            hash *= prime;
        }
        return hash == 0 ? 1 : hash;
    }

    private static TaskCompletionSource NewCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class RunningStatistics
    {
        private long _count;
        private double _sum;
        public double Minimum { get; private set; } = double.PositiveInfinity;
        public double Maximum { get; private set; } = double.NegativeInfinity;
        public double Average => _count == 0 ? 0 : _sum / _count;

        public void Add(double value)
        {
            _count++;
            _sum += value;
            Minimum = Math.Min(Minimum, value);
            Maximum = Math.Max(Maximum, value);
        }
    }
}

public sealed record LinuxSensorReading(
    uint SensorId,
    uint SensorInstance,
    string SensorName,
    uint EntryId,
    string EntryName,
    SensorCategory Category,
    double Value,
    double ValueMin,
    double ValueMax,
    double ValueAvg,
    string Unit,
    DateTime LastUpdate,
    string Source) : ISensorReading;

public sealed record LinuxSensorDescriptor(
    uint Id,
    uint Instance,
    string Name,
    string OriginalName) : ISensorDescriptor;
