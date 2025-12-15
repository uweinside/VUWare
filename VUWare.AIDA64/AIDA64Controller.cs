// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using VUWare.Lib.Sensors;

namespace VUWare.AIDA64
{
    /// <summary>
    /// High-level controller for AIDA64 sensor reading with VU1 dial integration.
    /// Provides periodic polling of AIDA64 and automatic dial updates based on sensor values.
    /// Also exposes ISensorProvider for consumers who want provider-agnostic access.
    /// </summary>
    /// <remarks>
    /// AIDA64 must be configured with shared memory enabled:
    /// File ? Preferences ? External Applications ? Shared Memory ? Enable Shared Memory
    /// </remarks>
    public class AIDA64Controller : IDisposable
    {
        private AIDA64Reader _reader;
        private AIDA64SensorProvider? _sensorProvider;
        private readonly Dictionary<string, DialSensorMapping> _dialMappings;
        private readonly Dictionary<string, AIDA64SensorReading?> _currentReadings;
        private CancellationTokenSource? _pollingCancellation;
        private Task? _pollingTask;
        private int _pollIntervalMs;
        private bool _disposed;
        private bool _isInitialized;

        /// <summary>
        /// Gets whether the controller is connected to AIDA64 shared memory.
        /// </summary>
        public bool IsConnected => _reader.IsConnected;

        /// <summary>
        /// Gets whether the controller has been initialized.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Gets or sets the polling interval in milliseconds.
        /// Minimum value is 100ms.
        /// </summary>
        public int PollIntervalMs
        {
            get => _pollIntervalMs;
            set => _pollIntervalMs = Math.Max(100, value);
        }

        /// <summary>
        /// Gets the ISensorProvider implementation for this controller.
        /// Use this when you need provider-agnostic sensor access.
        /// </summary>
        public ISensorProvider SensorProvider
        {
            get
            {
                if (_sensorProvider == null)
                {
                    _sensorProvider = new AIDA64SensorProvider(_reader);
                }
                return _sensorProvider;
            }
        }

        /// <summary>
        /// Delegate for sensor value change notifications.
        /// </summary>
        public delegate void SensorValueChanged(string sensorId, AIDA64SensorReading reading);

        /// <summary>
        /// Event fired when a sensor value changes.
        /// </summary>
        public event SensorValueChanged? OnSensorValueChanged;

        /// <summary>
        /// Creates a new AIDA64Controller instance.
        /// </summary>
        public AIDA64Controller()
        {
            _reader = new AIDA64Reader();
            _dialMappings = new Dictionary<string, DialSensorMapping>();
            _currentReadings = new Dictionary<string, AIDA64SensorReading?>();
            _pollIntervalMs = 1000; // Default 1000ms polling (AIDA64 default update rate)
            _isInitialized = false;
        }

        /// <summary>
        /// Internal constructor for accepting a pre-initialized reader.
        /// </summary>
        internal AIDA64Controller(AIDA64Reader reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _dialMappings = new Dictionary<string, DialSensorMapping>();
            _currentReadings = new Dictionary<string, AIDA64SensorReading?>();
            _pollIntervalMs = 1000;
            _isInitialized = false;
        }

        /// <summary>
        /// Attempts to connect to AIDA64 and initialize.
        /// AIDA64 must be running with Shared Memory enabled.
        /// </summary>
        /// <returns>True if connection was successful.</returns>
        public bool Connect()
        {
            if (!_reader.Connect())
            {
                return false;
            }

            _isInitialized = true;
            StartPolling();
            return true;
        }

        /// <summary>
        /// Connects using an already-connected AIDA64Reader instance.
        /// This is used when initialization has already succeeded and we have a reader.
        /// </summary>
        public bool ConnectWithReader(AIDA64Reader reader)
        {
            if (reader == null || !reader.IsConnected)
            {
                return false;
            }

            // Replace the reader
            _reader?.Dispose();
            _reader = reader;

            _isInitialized = true;
            StartPolling();
            return true;
        }

        /// <summary>
        /// Disconnects from AIDA64 and stops polling.
        /// </summary>
        public void Disconnect()
        {
            StopPolling();
            _reader.Disconnect();
            _isInitialized = false;
        }

        /// <summary>
        /// Registers a dial sensor mapping for automatic updates.
        /// </summary>
        /// <remarks>
        /// For AIDA64, use the sensor's Label or AIDA64 ID (e.g., "TCPU") in the EntryName field.
        /// The SensorName field can be used for the category (e.g., "Temperature") but is optional.
        /// </remarks>
        public void RegisterDialMapping(DialSensorMapping mapping)
        {
            if (string.IsNullOrWhiteSpace(mapping.Id))
                throw new ArgumentException("Mapping ID cannot be empty");

            _dialMappings[mapping.Id] = mapping;
            _currentReadings[mapping.Id] = null;
        }

        /// <summary>
        /// Unregisters a dial sensor mapping.
        /// </summary>
        public void UnregisterDialMapping(string mappingId)
        {
            _dialMappings.Remove(mappingId);
            _currentReadings.Remove(mappingId);
        }

        /// <summary>
        /// Gets all registered dial mappings.
        /// </summary>
        public IReadOnlyDictionary<string, DialSensorMapping> GetAllMappings()
        {
            return _dialMappings.AsReadOnly();
        }

        /// <summary>
        /// Gets the current reading for a dial mapping.
        /// </summary>
        public AIDA64SensorReading? GetCurrentReading(string mappingId)
        {
            _currentReadings.TryGetValue(mappingId, out var reading);
            return reading;
        }

        /// <summary>
        /// Gets all current sensor readings from AIDA64.
        /// </summary>
        public List<AIDA64SensorReading> GetAllSensorReadings()
        {
            var rawReadings = _reader.ReadAllSensorReadings();
            return rawReadings.Select(AIDA64SensorReading.FromRaw).ToList();
        }

        /// <summary>
        /// Gets the percentage value for a dial mapping based on current sensor reading.
        /// </summary>
        public byte? GetDialPercentage(string mappingId)
        {
            if (!_currentReadings.TryGetValue(mappingId, out var reading) || reading == null)
                return null;

            if (!_dialMappings.TryGetValue(mappingId, out var mapping))
                return null;

            return mapping.GetPercentage(reading.Value);
        }

        /// <summary>
        /// Gets the current sensor status for a dial mapping.
        /// </summary>
        public AIDA64SensorStatus? GetSensorStatus(string mappingId)
        {
            if (!_currentReadings.TryGetValue(mappingId, out var reading) || reading == null)
                return null;

            if (!_dialMappings.TryGetValue(mappingId, out var mapping))
                return null;

            return new AIDA64SensorStatus
            {
                MappingId = mappingId,
                SensorReading = reading,
                Percentage = mapping.GetPercentage(reading.Value),
                IsCritical = mapping.IsCritical(reading.Value),
                IsWarning = mapping.IsWarning(reading.Value)
            };
        }

        /// <summary>
        /// Starts the periodic polling of AIDA64.
        /// Uses dedicated thread with high priority to ensure responsiveness under 100% CPU load.
        /// </summary>
        private void StartPolling()
        {
            if (_pollingTask != null && !_pollingTask.IsCompleted)
                return;

            _pollingCancellation = new CancellationTokenSource();

            // Use dedicated thread instead of Task.Run to avoid thread pool starvation
            var pollingThread = new Thread(() => PollingLoop(_pollingCancellation.Token))
            {
                Name = "AIDA64 Polling",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };

            pollingThread.Start();
            _pollingTask = Task.CompletedTask;
        }

        /// <summary>
        /// Stops the periodic polling.
        /// </summary>
        private void StopPolling()
        {
            _pollingCancellation?.Cancel();
            try { _pollingTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
        }

        /// <summary>
        /// Polling loop that reads AIDA64 at regular intervals.
        /// </summary>
        private async void PollingLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && IsConnected)
                {
                    try
                    {
                        var readings = GetAllSensorReadings();

                        // Update current readings for registered mappings
                        foreach (var mapping in _dialMappings.Values)
                        {
                            AIDA64SensorReading? reading = null;

                            // AIDA64 matching strategy:
                            // 1. Try to match by AIDA64 ID (EntryName contains the ID like "TCPU")
                            // 2. Fall back to matching by label (human-readable name)
                            reading = readings.FirstOrDefault(r =>
                                r.AIDA64Id.Equals(mapping.EntryName, StringComparison.OrdinalIgnoreCase));

                            if (reading == null)
                            {
                                // Try matching by label
                                reading = readings.FirstOrDefault(r =>
                                    r.EntryName.Equals(mapping.EntryName, StringComparison.OrdinalIgnoreCase));
                            }

                            if (reading == null && !string.IsNullOrEmpty(mapping.SensorName))
                            {
                                // Try matching by category + entry name
                                reading = readings.FirstOrDefault(r =>
                                    r.SensorName.Equals(mapping.SensorName, StringComparison.OrdinalIgnoreCase) &&
                                    r.EntryName.Equals(mapping.EntryName, StringComparison.OrdinalIgnoreCase));
                            }

                            if (reading != null)
                            {
                                var previous = _currentReadings.TryGetValue(mapping.Id, out var prev) ? prev : null;
                                _currentReadings[mapping.Id] = reading;

                                // Fire event only if value changed significantly (0.1% threshold)
                                if (previous == null || Math.Abs(previous.Value - reading.Value) > 0.1)
                                {
                                    OnSensorValueChanged?.Invoke(mapping.Id, reading);
                                }
                            }
                        }

                        await Task.Delay(_pollIntervalMs, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AIDA64Controller] Polling error: {ex.Message}");
                        await Task.Delay(1000, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AIDA64Controller] Polling loop failure: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the poll interval dynamically without restarting polling.
        /// </summary>
        /// <param name="newIntervalMs">New polling interval in milliseconds (minimum 100ms)</param>
        public void UpdatePollInterval(int newIntervalMs)
        {
            _pollIntervalMs = Math.Max(100, newIntervalMs);
            System.Diagnostics.Debug.WriteLine($"[AIDA64Controller] Poll interval updated to {_pollIntervalMs}ms");
        }

        /// <summary>
        /// Updates an existing dial mapping with new configuration.
        /// </summary>
        public void UpdateDialMapping(string mappingId, DialSensorMapping newMapping)
        {
            if (string.IsNullOrWhiteSpace(mappingId))
                throw new ArgumentException("Mapping ID cannot be empty", nameof(mappingId));

            if (string.IsNullOrWhiteSpace(newMapping.Id))
                throw new ArgumentException("New mapping ID cannot be empty", nameof(newMapping));

            _dialMappings[mappingId] = newMapping;
            System.Diagnostics.Debug.WriteLine($"[AIDA64Controller] Updated mapping for {mappingId}");
        }

        /// <summary>
        /// Clears all registered dial mappings.
        /// </summary>
        public void ClearAllMappings()
        {
            int count = _dialMappings.Count;
            _dialMappings.Clear();
            _currentReadings.Clear();
            System.Diagnostics.Debug.WriteLine($"[AIDA64Controller] Cleared {count} dial mapping(s)");
        }

        /// <summary>
        /// Registers multiple dial mappings at once.
        /// </summary>
        public void RegisterMappings(IEnumerable<DialSensorMapping> mappings)
        {
            if (mappings == null)
                throw new ArgumentNullException(nameof(mappings));

            int count = 0;
            foreach (var mapping in mappings)
            {
                RegisterDialMapping(mapping);
                count++;
            }

            System.Diagnostics.Debug.WriteLine($"[AIDA64Controller] Registered {count} dial mapping(s)");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                StopPolling();
                _reader?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Represents the current status of an AIDA64 sensor for dial display.
    /// </summary>
    public class AIDA64SensorStatus
    {
        /// <summary>ID of the dial mapping</summary>
        public string MappingId { get; set; } = string.Empty;

        /// <summary>The sensor reading data</summary>
        public AIDA64SensorReading? SensorReading { get; set; }

        /// <summary>Percentage value for dial (0-100)</summary>
        public byte Percentage { get; set; }

        /// <summary>Whether the value is in critical range</summary>
        public bool IsCritical { get; set; }

        /// <summary>Whether the value is in warning range</summary>
        public bool IsWarning { get; set; }

        /// <summary>Gets the recommended backlight color based on status.</summary>
        public (byte Red, byte Green, byte Blue) GetRecommendedColor()
        {
            if (IsCritical)
                return (100, 0, 0);   // Red
            if (IsWarning)
                return (100, 50, 0);  // Orange
            return (0, 100, 0);       // Green (normal)
        }
    }
}
