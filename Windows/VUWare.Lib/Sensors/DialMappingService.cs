// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace VUWare.Lib.Sensors
{
    /// <summary>
    /// Provider-agnostic implementation of IDialMappingService.
    /// Works with any ISensorProvider to map sensors to VU1 dials.
    /// </summary>
    public class DialMappingService : IDialMappingService
    {
        private readonly ISensorProvider _sensorProvider;
        private readonly Dictionary<string, DialSensorMapping> _mappings;
        private readonly Dictionary<string, ISensorReading?> _currentReadings;
        private readonly object _lock = new();
        private bool _disposed;
        private int _pollIntervalMs = 500;

        /// <inheritdoc />
        public ISensorProvider SensorProvider => _sensorProvider;

        /// <inheritdoc />
        public bool IsConnected => _sensorProvider.IsConnected;

        /// <inheritdoc />
        public event Action<string, ISensorReading>? OnSensorValueChanged;

        /// <summary>
        /// Creates a new DialMappingService with the specified sensor provider.
        /// </summary>
        /// <param name="sensorProvider">The sensor provider to use for reading data.</param>
        public DialMappingService(ISensorProvider sensorProvider)
        {
            _sensorProvider = sensorProvider ?? throw new ArgumentNullException(nameof(sensorProvider));
            _mappings = new Dictionary<string, DialSensorMapping>();
            _currentReadings = new Dictionary<string, ISensorReading?>();
        }

        /// <inheritdoc />
        public void RegisterMapping(DialSensorMapping mapping)
        {
            if (mapping == null)
                throw new ArgumentNullException(nameof(mapping));
            if (string.IsNullOrWhiteSpace(mapping.Id))
                throw new ArgumentException("Mapping ID cannot be empty", nameof(mapping));

            lock (_lock)
            {
                _mappings[mapping.Id] = mapping;
                _currentReadings[mapping.Id] = null;
                Debug.WriteLine($"[DialMappingService] Registered mapping: {mapping.Id} -> {mapping.SensorName}/{mapping.EntryName}");
            }
        }

        /// <inheritdoc />
        public void RegisterMappings(IEnumerable<DialSensorMapping> mappings)
        {
            if (mappings == null)
                throw new ArgumentNullException(nameof(mappings));

            foreach (var mapping in mappings)
            {
                RegisterMapping(mapping);
            }
        }

        /// <inheritdoc />
        public void UnregisterMapping(string mappingId)
        {
            lock (_lock)
            {
                _mappings.Remove(mappingId);
                _currentReadings.Remove(mappingId);
                Debug.WriteLine($"[DialMappingService] Unregistered mapping: {mappingId}");
            }
        }

        /// <inheritdoc />
        public void ClearAllMappings()
        {
            lock (_lock)
            {
                int count = _mappings.Count;
                _mappings.Clear();
                _currentReadings.Clear();
                Debug.WriteLine($"[DialMappingService] Cleared {count} mapping(s)");
            }
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<string, DialSensorMapping> GetAllMappings()
        {
            lock (_lock)
            {
                return new Dictionary<string, DialSensorMapping>(_mappings);
            }
        }

        /// <inheritdoc />
        public DialSensorStatus? GetStatus(string mappingId)
        {
            var reading = GetReading(mappingId);
            if (reading == null)
                return null;

            DialSensorMapping? mapping;
            lock (_lock)
            {
                if (!_mappings.TryGetValue(mappingId, out mapping))
                    return null;
            }

            return new DialSensorStatus
            {
                MappingId = mappingId,
                SensorReading = reading,
                Percentage = mapping.GetPercentage(reading.Value),
                IsCritical = mapping.IsCritical(reading.Value),
                IsWarning = mapping.IsWarning(reading.Value)
            };
        }

        /// <inheritdoc />
        public ISensorReading? GetReading(string mappingId)
        {
            DialSensorMapping? mapping;
            lock (_lock)
            {
                if (!_mappings.TryGetValue(mappingId, out mapping))
                    return null;
            }

            // Get all readings from provider
            var allReadings = _sensorProvider.GetAllReadings();
            if (allReadings == null || allReadings.Count == 0)
                return null;

            // Find matching reading using the mapping configuration
            ISensorReading? matchedReading = FindMatchingReading(allReadings, mapping);

            if (matchedReading != null)
            {
                ISensorReading? previousReading;
                lock (_lock)
                {
                    _currentReadings.TryGetValue(mappingId, out previousReading);
                    _currentReadings[mappingId] = matchedReading;
                }

                // Fire event if value changed significantly
                if (previousReading == null || Math.Abs(previousReading.Value - matchedReading.Value) > 0.1)
                {
                    OnSensorValueChanged?.Invoke(mappingId, matchedReading);
                }
            }

            return matchedReading;
        }

        /// <summary>
        /// Finds a sensor reading that matches the mapping configuration.
        /// Uses precise matching (ID/instance) when available, falls back to name matching.
        /// </summary>
        private static ISensorReading? FindMatchingReading(IReadOnlyList<ISensorReading> readings, DialSensorMapping mapping)
        {
            // Strategy 1: Precise match using SensorId + SensorInstance + EntryId (if all are set)
            if (mapping.SensorId != 0 && mapping.EntryId != 0)
            {
                var preciseMatch = readings.FirstOrDefault(r =>
                    r.SensorId == mapping.SensorId &&
                    r.SensorInstance == mapping.SensorInstance &&
                    r.EntryId == mapping.EntryId);

                if (preciseMatch != null)
                    return preciseMatch;
            }

            // Strategy 2: Match by SensorId + SensorInstance + EntryName
            if (mapping.SensorId != 0)
            {
                var idMatch = readings.FirstOrDefault(r =>
                    r.SensorId == mapping.SensorId &&
                    r.SensorInstance == mapping.SensorInstance &&
                    r.EntryName.Equals(mapping.EntryName, StringComparison.OrdinalIgnoreCase));

                if (idMatch != null)
                    return idMatch;
            }

            // Strategy 3: Match by SensorName + EntryName (fallback for legacy configs)
            var nameMatch = readings.FirstOrDefault(r =>
                r.SensorName.Equals(mapping.SensorName, StringComparison.OrdinalIgnoreCase) &&
                r.EntryName.Equals(mapping.EntryName, StringComparison.OrdinalIgnoreCase));

            return nameMatch;
        }

        /// <inheritdoc />
        public void UpdatePollInterval(int intervalMs)
        {
            _pollIntervalMs = Math.Max(100, intervalMs);
            Debug.WriteLine($"[DialMappingService] Poll interval updated to {_pollIntervalMs}ms");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                ClearAllMappings();
                _disposed = true;
            }
        }
    }
}
