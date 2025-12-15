// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using VUWare.Lib.Sensors;

namespace VUWare.AIDA64
{
    /// <summary>
    /// AIDA64 implementation of ISensorProvider.
    /// Reads sensor data from AIDA64's shared memory export and provides it through the common interface.
    /// </summary>
    /// <remarks>
    /// AIDA64 must be configured with shared memory enabled:
    /// File ? Preferences ? External Applications ? Shared Memory ? Enable Shared Memory
    /// </remarks>
    public class AIDA64SensorProvider : ISensorProvider
    {
        private readonly AIDA64Reader _reader;
        private bool _disposed;
        private List<AIDA64SensorDescriptor>? _cachedSensors;
        private List<AIDA64SensorReading>? _cachedReadings;
        private DateTime _lastSensorCacheTime = DateTime.MinValue;
        private DateTime _lastReadingCacheTime = DateTime.MinValue;
        private readonly TimeSpan _sensorCacheExpiry = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _readingCacheExpiry = TimeSpan.FromMilliseconds(100);

        /// <inheritdoc />
        public string ProviderName => "AIDA64";

        /// <inheritdoc />
        public bool IsConnected => _reader.IsConnected;

        /// <inheritdoc />
        public event EventHandler<SensorProviderConnectionEventArgs>? ConnectionStatusChanged;

        /// <summary>
        /// Creates a new AIDA64 sensor provider instance.
        /// </summary>
        public AIDA64SensorProvider()
        {
            _reader = new AIDA64Reader();
        }

        /// <summary>
        /// Creates a new AIDA64 sensor provider with an existing reader instance.
        /// </summary>
        /// <param name="reader">Existing AIDA64Reader instance.</param>
        internal AIDA64SensorProvider(AIDA64Reader reader)
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
                    result ? "Connected to AIDA64 shared memory" : "Disconnected from AIDA64"));
            }

            return result;
        }

        /// <inheritdoc />
        public void Disconnect()
        {
            bool wasConnected = _reader.IsConnected;
            _reader.Disconnect();
            _cachedSensors = null;
            _cachedReadings = null;

            if (wasConnected)
            {
                ConnectionStatusChanged?.Invoke(this, new SensorProviderConnectionEventArgs(
                    false, "Disconnected from AIDA64"));
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

            var readings = GetCachedReadings();

            // AIDA64 groups readings by category (Temperature, Voltage, Fan, etc.)
            // Each category becomes a "sensor" with individual readings as "entries"
            // This matches the Settings UI pattern used by HWInfo64
            _cachedSensors = readings
                .GroupBy(r => r.SensorName)  // Group by category display name
                .Select(g => new AIDA64SensorDescriptor
                {
                    Id = (uint)g.Key.GetHashCode(),
                    Instance = 0,
                    Name = g.Key,  // Category name (e.g., "Temperatures", "Voltages")
                    OriginalName = g.Key
                })
                .ToList();

            _lastSensorCacheTime = DateTime.Now;
            return _cachedSensors;
        }

        /// <inheritdoc />
        public IReadOnlyList<ISensorReading> GetAllReadings()
        {
            if (!_reader.IsConnected)
                return Array.Empty<ISensorReading>();

            return GetCachedReadings();
        }

        /// <inheritdoc />
        public IReadOnlyList<ISensorReading> GetReadingsByType(SensorCategory type)
        {
            if (!_reader.IsConnected)
                return Array.Empty<ISensorReading>();

            var readings = GetCachedReadings();
            return readings.Where(r => r.Category == type).ToList<ISensorReading>();
        }

        /// <inheritdoc />
        public ISensorReading? GetReading(string sensorName, string entryName)
        {
            if (!_reader.IsConnected)
                return null;

            var readings = GetCachedReadings();

            // AIDA64 uses Label (entryName) as the primary identifier
            // Try matching by exact label first
            var reading = readings.FirstOrDefault(r =>
                r.EntryName.Equals(entryName, StringComparison.OrdinalIgnoreCase));

            // If not found by label, try matching by AIDA64 ID
            reading ??= readings.FirstOrDefault(r =>
                r.AIDA64Id.Equals(entryName, StringComparison.OrdinalIgnoreCase));

            // If sensorName is provided, also try to match category
            if (reading == null && !string.IsNullOrEmpty(sensorName))
            {
                reading = readings.FirstOrDefault(r =>
                    r.SensorName.Equals(sensorName, StringComparison.OrdinalIgnoreCase) &&
                    (r.EntryName.Equals(entryName, StringComparison.OrdinalIgnoreCase) ||
                     r.AIDA64Id.Equals(entryName, StringComparison.OrdinalIgnoreCase)));
            }

            return reading;
        }

        /// <summary>
        /// Gets a sensor reading by its AIDA64 sensor ID (e.g., "TCPU", "SCPUCLK").
        /// </summary>
        /// <param name="aida64Id">The AIDA64 sensor ID.</param>
        /// <returns>The sensor reading if found, null otherwise.</returns>
        public AIDA64SensorReading? GetReadingById(string aida64Id)
        {
            if (!_reader.IsConnected || string.IsNullOrEmpty(aida64Id))
                return null;

            var readings = GetCachedReadings();
            return readings.FirstOrDefault(r =>
                r.AIDA64Id.Equals(aida64Id, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc />
        public IReadOnlyList<SensorCategory> GetAvailableCategories()
        {
            if (!_reader.IsConnected)
                return Array.Empty<SensorCategory>();

            var readings = GetCachedReadings();
            return readings
                .Select(r => r.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        /// <summary>
        /// Gets all available AIDA64-specific categories.
        /// </summary>
        /// <returns>Collection of AIDA64 categories present in current readings.</returns>
        public IReadOnlyList<AIDA64SensorCategory> GetAvailableAIDA64Categories()
        {
            if (!_reader.IsConnected)
                return Array.Empty<AIDA64SensorCategory>();

            var readings = GetCachedReadings();
            return readings
                .Select(r => r.OriginalCategory)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        /// <summary>
        /// Gets readings from cache or refreshes if expired.
        /// </summary>
        private List<AIDA64SensorReading> GetCachedReadings()
        {
            // Return cached readings if still valid
            if (_cachedReadings != null && DateTime.Now - _lastReadingCacheTime < _readingCacheExpiry)
                return _cachedReadings;

            var rawReadings = _reader.ReadAllSensorReadings();
            _cachedReadings = rawReadings
                .Select(AIDA64SensorReading.FromRaw)
                .ToList();
            _lastReadingCacheTime = DateTime.Now;

            return _cachedReadings;
        }

        /// <summary>
        /// Gets the underlying AIDA64Reader instance for advanced operations.
        /// </summary>
        internal AIDA64Reader GetReader() => _reader;

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
}
