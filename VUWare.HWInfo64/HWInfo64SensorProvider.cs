// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using VUWare.Lib.Sensors;

namespace VUWare.HWInfo64
{
    /// <summary>
    /// HWInfo64 implementation of the ISensorProvider interface.
    /// Wraps HWiNFOReader to provide sensor data through the common interface.
    /// </summary>
    public class HWInfo64SensorProvider : ISensorProvider
    {
        private readonly HWiNFOReader _reader;
        private bool _disposed;
        private List<SensorDescriptor>? _cachedSensors;
        private DateTime _lastSensorCacheTime = DateTime.MinValue;
        private readonly TimeSpan _sensorCacheExpiry = TimeSpan.FromSeconds(5);

        /// <inheritdoc />
        public string ProviderName => "HWInfo64";

        /// <inheritdoc />
        public bool IsConnected => _reader.IsConnected;

        /// <inheritdoc />
        public event EventHandler<SensorProviderConnectionEventArgs>? ConnectionStatusChanged;

        /// <summary>
        /// Creates a new HWInfo64 sensor provider instance.
        /// </summary>
        public HWInfo64SensorProvider()
        {
            _reader = new HWiNFOReader();
        }

        /// <summary>
        /// Creates a new HWInfo64 sensor provider with an existing reader instance.
        /// </summary>
        /// <param name="reader">Existing HWiNFOReader instance.</param>
        internal HWInfo64SensorProvider(HWiNFOReader reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        /// <inheritdoc />
        public bool Connect()
        {
            bool wasConnected = _reader.IsConnected;
            bool result = _reader.Connect();

            if (result != wasConnected)
            {
                ConnectionStatusChanged?.Invoke(this, new SensorProviderConnectionEventArgs(
                    result, 
                    result ? "Connected to HWInfo64 shared memory" : "Disconnected from HWInfo64"));
            }

            return result;
        }

        /// <inheritdoc />
        public void Disconnect()
        {
            bool wasConnected = _reader.IsConnected;
            _reader.Disconnect();
            _cachedSensors = null;

            if (wasConnected)
            {
                ConnectionStatusChanged?.Invoke(this, new SensorProviderConnectionEventArgs(
                    false, "Disconnected from HWInfo64"));
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<ISensorDescriptor> GetSensors()
        {
            if (!_reader.IsConnected)
                return Array.Empty<ISensorDescriptor>();

            // Use cached sensors if still valid
            if (_cachedSensors != null && DateTime.Now - _lastSensorCacheTime < _sensorCacheExpiry)
                return _cachedSensors;

            var hwSensors = _reader.ReadAllSensors();
            if (hwSensors == null)
                return Array.Empty<ISensorDescriptor>();

            _cachedSensors = hwSensors.Select(s => new SensorDescriptor
            {
                Id = s.id,
                Instance = s.instance,
                Name = string.IsNullOrWhiteSpace(s.name_user) ? s.name_original : s.name_user,
                OriginalName = s.name_original
            }).ToList();

            _lastSensorCacheTime = DateTime.Now;
            return _cachedSensors;
        }

        /// <inheritdoc />
        public IReadOnlyList<ISensorReading> GetAllReadings()
        {
            if (!_reader.IsConnected)
                return Array.Empty<ISensorReading>();

            return _reader.ReadAllSensorReadings();
        }

        /// <inheritdoc />
        public IReadOnlyList<ISensorReading> GetReadingsByType(SensorCategory type)
        {
            if (!_reader.IsConnected)
                return Array.Empty<ISensorReading>();

            var readings = _reader.ReadAllSensorReadings();
            return readings.Where(r => r.Category == type).ToList();
        }

        /// <inheritdoc />
        public ISensorReading? GetReading(string sensorName, string entryName)
        {
            if (!_reader.IsConnected)
                return null;

            var readings = _reader.ReadAllSensorReadings();
            return readings.FirstOrDefault(r =>
                r.SensorName.Equals(sensorName, StringComparison.OrdinalIgnoreCase) &&
                r.EntryName.Equals(entryName, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc />
        public IReadOnlyList<SensorCategory> GetAvailableCategories()
        {
            if (!_reader.IsConnected)
                return Array.Empty<SensorCategory>();

            var readings = _reader.ReadAllSensorReadings();
            return readings
                .Select(r => r.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        /// <summary>
        /// Gets the underlying HWiNFOReader instance for advanced operations.
        /// </summary>
        internal HWiNFOReader GetReader() => _reader;

        public void Dispose()
        {
            if (!_disposed)
            {
                Disconnect();
                _reader.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Implementation of ISensorDescriptor for HWInfo64 sensors.
    /// </summary>
    internal class SensorDescriptor : ISensorDescriptor
    {
        public uint Id { get; init; }
        public uint Instance { get; init; }
        public string Name { get; init; } = string.Empty;
        public string OriginalName { get; init; } = string.Empty;
    }
}
