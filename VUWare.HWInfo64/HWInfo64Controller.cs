using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VUWare.HWInfo64
{
    /// <summary>
    /// High-level controller for HWInfo64 sensor reading with VU1 dial integration.
    /// Provides periodic polling of HWInfo64 and automatic dial updates based on sensor values.
    /// </summary>
    public class HWInfo64Controller : IDisposable
    {
        private readonly HWiNFOReader _reader;
        private readonly Dictionary<string, DialSensorMapping> _dialMappings;
        private readonly Dictionary<string, SensorReading?> _currentReadings;
        private CancellationTokenSource? _pollingCancellation;
        private Task? _pollingTask;
        private int _pollIntervalMs;
        private bool _disposed;
        private bool _isInitialized;

        public bool IsConnected => _reader.IsConnected;
        public bool IsInitialized => _isInitialized;
        public int PollIntervalMs
        {
            get => _pollIntervalMs;
            set => _pollIntervalMs = Math.Max(100, value); // Minimum 100ms
        }

        public delegate void SensorValueChanged(string sensorId, SensorReading reading);
        public event SensorValueChanged? OnSensorValueChanged;

        public HWInfo64Controller()
        {
            _reader = new HWiNFOReader();
            _dialMappings = new Dictionary<string, DialSensorMapping>();
            _currentReadings = new Dictionary<string, SensorReading?>();
            _pollIntervalMs = 500; // Default 500ms polling interval
            _isInitialized = false;
        }

        /// <summary>
        /// Attempts to connect to HWInfo64 and initialize.
        /// HWInfo64 must be running in Sensors-only mode with Shared Memory Support enabled.
        /// </summary>
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
        /// Disconnects from HWInfo64 and stops polling.
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
        public SensorReading? GetCurrentReading(string mappingId)
        {
            _currentReadings.TryGetValue(mappingId, out var reading);
            return reading;
        }

        /// <summary>
        /// Gets all current sensor readings from HWInfo64.
        /// </summary>
        public List<SensorReading> GetAllSensorReadings()
        {
            return _reader.ReadAllSensorReadings();
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
        public SensorStatus? GetSensorStatus(string mappingId)
        {
            if (!_currentReadings.TryGetValue(mappingId, out var reading) || reading == null)
                return null;

            if (!_dialMappings.TryGetValue(mappingId, out var mapping))
                return null;

            var status = new SensorStatus
            {
                MappingId = mappingId,
                SensorReading = reading,
                Percentage = mapping.GetPercentage(reading.Value),
                IsCritical = mapping.IsCritical(reading.Value),
                IsWarning = mapping.IsWarning(reading.Value)
            };

            return status;
        }

        /// <summary>
        /// Starts the periodic polling of HWInfo64.
        /// </summary>
        private void StartPolling()
        {
            if (_pollingTask != null && !_pollingTask.IsCompleted)
                return;

            _pollingCancellation = new CancellationTokenSource();
            _pollingTask = Task.Run(() => PollingLoop(_pollingCancellation.Token));
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
        /// Polling loop that reads HWInfo64 at regular intervals.
        /// </summary>
        private void PollingLoop(CancellationToken cancellationToken)
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
                            var reading = readings.FirstOrDefault(r =>
                                r.SensorName.Equals(mapping.SensorName, StringComparison.OrdinalIgnoreCase) &&
                                r.EntryName.Equals(mapping.EntryName, StringComparison.OrdinalIgnoreCase));

                            if (reading != null)
                            {
                                var previous = _currentReadings.ContainsKey(mapping.Id) ? _currentReadings[mapping.Id] : null;
                                _currentReadings[mapping.Id] = reading;

                                // Fire event if value changed
                                if (previous == null || Math.Abs(previous.Value - reading.Value) > 0.01)
                                {
                                    OnSensorValueChanged?.Invoke(mapping.Id, reading);
                                }
                            }
                        }

                        Task.Delay(_pollIntervalMs, cancellationToken).Wait(cancellationToken);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Polling error: {ex.Message}");
                        Task.Delay(1000, cancellationToken).Wait(cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Polling loop failure: {ex.Message}");
            }
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
    /// Represents the current status of a sensor for dial display.
    /// </summary>
    public class SensorStatus
    {
        /// <summary>ID of the dial mapping</summary>
        public string MappingId { get; set; } = string.Empty;

        /// <summary>The sensor reading data</summary>
        public SensorReading? SensorReading { get; set; }

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
