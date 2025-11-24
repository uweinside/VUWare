using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VUWare.App.Models;
using VUWare.HWInfo64;
using VUWare.Lib;

namespace VUWare.App.Services
{
    /// <summary>
    /// Manages real-time monitoring of HWInfo64 sensors and updates VU1 dials.
    /// Runs on a background thread and periodically polls sensor data, applies thresholds,
    /// and updates dial positions and colors based on sensor values.
    /// </summary>
    public class SensorMonitoringService : IDisposable
    {
        private readonly VU1Controller _vuController;
        private readonly HWInfo64Controller _hwInfoController;
        private readonly DialsConfiguration _config;
        private readonly Dictionary<string, DialMonitoringState> _dialStates;
        private CancellationTokenSource? _monitoringCts;
        private Task? _monitoringTask;
        private bool _disposed;
        private bool _isMonitoring;

        /// <summary>
        /// Tracks the state of a single dial during monitoring.
        /// </summary>
        private class DialMonitoringState
        {
            public string DialUid { get; set; } = string.Empty;
            public DialConfig Config { get; set; } = null!;
            public SensorReading? LastReading { get; set; }
            public byte LastPercentage { get; set; }
            public string LastColor { get; set; } = string.Empty;
            public DateTime LastUpdate { get; set; }
            public int UpdateCount { get; set; }
        }

        /// <summary>
        /// Event fired when a dial's sensor value is updated.
        /// </summary>
        public event Action<string, DialSensorUpdate>? OnDialUpdated;

        /// <summary>
        /// Event fired when monitoring encounters an error.
        /// </summary>
        public event Action<string>? OnError;

        public bool IsMonitoring => _isMonitoring;

        public SensorMonitoringService(VU1Controller vuController, HWInfo64Controller hwInfoController, DialsConfiguration config)
        {
            _vuController = vuController ?? throw new ArgumentNullException(nameof(vuController));
            _hwInfoController = hwInfoController ?? throw new ArgumentNullException(nameof(hwInfoController));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _dialStates = new Dictionary<string, DialMonitoringState>();
        }

        /// <summary>
        /// Starts the monitoring loop on a background thread.
        /// </summary>
        public void Start()
        {
            if (_isMonitoring)
                return;

            // Initialize monitoring states for all enabled dials
            _dialStates.Clear();
            foreach (var dialConfig in _config.Dials)
            {
                if (dialConfig.Enabled)
                {
                    _dialStates[dialConfig.DialUid] = new DialMonitoringState
                    {
                        DialUid = dialConfig.DialUid,
                        Config = dialConfig,
                        LastPercentage = 0,
                        LastColor = dialConfig.ColorConfig.NormalColor,
                        LastUpdate = DateTime.Now,
                        UpdateCount = 0
                    };
                }
            }

            _monitoringCts = new CancellationTokenSource();
            _monitoringTask = Task.Run(async () => 
            {
                // Send initial values to all dials before starting the loop
                await InitializeDials(_monitoringCts.Token);
                // Then start the monitoring loop
                MonitoringLoop(_monitoringCts.Token);
            });
            _isMonitoring = true;
        }

        /// <summary>
        /// Initializes all dials with their current sensor values.
        /// Called when monitoring starts to immediately display sensor data.
        /// </summary>
        private async Task InitializeDials(CancellationToken cancellationToken)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("InitializeDials: Starting initial sensor read and dial update");
                
                // Wait a short moment for HWInfo64 to have initial data
                await Task.Delay(500, cancellationToken);

                foreach (var state in _dialStates.Values)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        // Get initial sensor reading
                        var status = _hwInfoController.GetSensorStatus(state.DialUid);
                        if (status == null)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"InitializeDials: No initial reading for {state.Config.DisplayName}");
                            continue;
                        }

                        state.LastReading = status.SensorReading;
                        state.LastPercentage = status.Percentage;

                        // Determine color based on thresholds
                        string initialColor = state.Config.GetColorForValue(status.SensorReading.Value);
                        state.LastColor = initialColor;

                        // Send initial position
                        bool positionSuccess = await _vuController.SetDialPercentageAsync(
                            state.DialUid, status.Percentage);
                        if (!positionSuccess)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"InitializeDials: Failed to set initial position for {state.Config.DisplayName}");
                            continue;
                        }

                        // Send initial color
                        var color = GetColorFromName(initialColor);
                        if (color != null)
                        {
                            bool colorSuccess = await _vuController.SetBacklightColorAsync(
                                state.DialUid, color);
                            if (!colorSuccess)
                            {
                                System.Diagnostics.Debug.WriteLine(
                                    $"InitializeDials: Failed to set initial color for {state.Config.DisplayName}");
                                continue;
                            }
                        }

                        state.UpdateCount++;
                        state.LastUpdate = DateTime.Now;

                        System.Diagnostics.Debug.WriteLine(
                            $"InitializeDials: {state.Config.DisplayName} initialized ? {status.Percentage}% ({initialColor})");

                        // Raise event for UI update
                        RaiseDialUpdated(state);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"InitializeDials: Error initializing dial {state.DialUid}: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine("InitializeDials: Complete");
            }
            catch (OperationCanceledException)
            {
                // Initialization was cancelled
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeDials: Failure: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops the monitoring loop.
        /// </summary>
        public void Stop()
        {
            _monitoringCts?.Cancel();
            try { _monitoringTask?.Wait(TimeSpan.FromSeconds(5)); } catch { }
            _isMonitoring = false;
        }

        /// <summary>
        /// Gets the current monitoring state for a dial.
        /// </summary>
        public DialSensorUpdate? GetDialStatus(string dialUid)
        {
            if (!_dialStates.TryGetValue(dialUid, out var state) || state.LastReading == null)
                return null;

            // Debug output
            System.Diagnostics.Debug.WriteLine(
                $"GetDialStatus: {state.Config.DisplayName} ? {state.LastPercentage}% ({state.LastColor})");

            return new DialSensorUpdate
            {
                DialUid = dialUid,
                DisplayName = state.Config.DisplayName,
                SensorName = state.Config.SensorName,
                EntryName = state.Config.EntryName,
                SensorValue = state.LastReading.Value,
                SensorUnit = state.LastReading.Unit,
                DialPercentage = state.LastPercentage,
                CurrentColor = state.LastColor,
                IsWarning = state.Config.WarningThreshold.HasValue && 
                           state.LastReading.Value >= state.Config.WarningThreshold.Value &&
                           (!state.Config.CriticalThreshold.HasValue || 
                            state.LastReading.Value < state.Config.CriticalThreshold.Value),
                IsCritical = state.Config.CriticalThreshold.HasValue && 
                            state.LastReading.Value >= state.Config.CriticalThreshold.Value,
                LastUpdate = state.LastUpdate,
                UpdateCount = state.UpdateCount
            };
        }

        /// <summary>
        /// Background monitoring loop that reads sensors and updates dials.
        /// </summary>
        private async void MonitoringLoop(CancellationToken cancellationToken)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("MonitoringLoop: Started");
                int cycleCount = 0;

                while (!cancellationToken.IsCancellationRequested && _isMonitoring)
                {
                    try
                    {
                        cycleCount++;
                        bool anyUpdated = false;

                        // Process each enabled dial
                        foreach (var state in _dialStates.Values)
                        {
                            if (cancellationToken.IsCancellationRequested)
                                break;

                            try
                            {
                                bool updated = await UpdateDialAsync(state, cancellationToken);
                                if (updated)
                                    anyUpdated = true;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error updating dial {state.DialUid}: {ex.Message}");
                            }
                        }

                        if (cycleCount % 10 == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"MonitoringLoop: Cycle {cycleCount}, Updated: {anyUpdated}");
                        }

                        // Sleep based on global update interval
                        await Task.Delay(_config.AppSettings.GlobalUpdateIntervalMs, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Monitoring loop cycle error: {ex.Message}");
                        RaiseError($"Monitoring loop error: {ex.Message}");
                        await Task.Delay(1000, cancellationToken);
                    }
                }

                System.Diagnostics.Debug.WriteLine("MonitoringLoop: Stopped");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Monitoring loop failure: {ex.Message}");
                RaiseError($"Monitoring failed: {ex.Message}");
            }
            finally
            {
                _isMonitoring = false;
            }
        }

        /// <summary>
        /// Updates a single dial based on current sensor reading.
        /// Returns true if the dial was updated.
        /// </summary>
        private async Task<bool> UpdateDialAsync(DialMonitoringState state, CancellationToken cancellationToken)
        {
            // Get current sensor reading
            var status = _hwInfoController.GetSensorStatus(state.DialUid);
            if (status == null)
            {
                // Sensor reading not available yet - this is normal on first cycles
                return false;
            }

            state.LastReading = status.SensorReading;
            state.LastPercentage = status.Percentage;

            // Determine color based on thresholds
            string newColor = state.Config.GetColorForValue(status.SensorReading.Value);

            // Only update if significant change (avoid unnecessary serial communication)
            bool needsUpdate = false;
            if (Math.Abs(state.LastPercentage - status.Percentage) > 0) // Position changed
                needsUpdate = true;
            if (state.LastColor != newColor) // Color changed
                needsUpdate = true;

            if (!needsUpdate)
            {
                // Still update the timestamp even if no change
                state.LastUpdate = DateTime.Now;
                return false;
            }

            // Update dial position
            bool positionSuccess = await _vuController.SetDialPercentageAsync(state.DialUid, status.Percentage);
            if (!positionSuccess)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update dial position for {state.DialUid}");
                return false;
            }

            // Update backlight color
            var color = GetColorFromName(newColor);
            if (color != null)
            {
                bool colorSuccess = await _vuController.SetBacklightColorAsync(state.DialUid, color);
                if (!colorSuccess)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to update dial color for {state.DialUid}");
                    return false;
                }
            }

            // Update state
            state.LastColor = newColor;
            state.LastUpdate = DateTime.Now;
            state.UpdateCount++;

            System.Diagnostics.Debug.WriteLine(
                $"Dial updated: {state.Config.DisplayName} ? {status.Percentage}% ({newColor})");

            // Raise event for UI update
            RaiseDialUpdated(state);
            
            return true;
        }

        /// <summary>
        /// Converts a color name to a NamedColor object.
        /// </summary>
        private NamedColor? GetColorFromName(string colorName)
        {
            return colorName switch
            {
                "Red" => Colors.Red,
                "Green" => Colors.Green,
                "Blue" => Colors.Blue,
                "Yellow" => Colors.Yellow,
                "Cyan" => Colors.Cyan,
                "Magenta" => Colors.Magenta,
                "Orange" => Colors.Orange,
                "Purple" => Colors.Purple,
                "Pink" => Colors.Pink,
                "White" => Colors.White,
                "Off" => Colors.Off,
                _ => null
            };
        }

        /// <summary>
        /// Raises the dial updated event on the UI thread.
        /// </summary>
        private void RaiseDialUpdated(DialMonitoringState state)
        {
            try
            {
                var update = GetDialStatus(state.DialUid);
                if (update != null)
                {
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        OnDialUpdated?.Invoke(state.DialUid, update);
                    });
                }
            }
            catch
            {
                // Ignore dispatcher errors
            }
        }

        /// <summary>
        /// Raises the error event on the UI thread.
        /// </summary>
        private void RaiseError(string errorMessage)
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    OnError?.Invoke(errorMessage);
                });
            }
            catch
            {
                OnError?.Invoke(errorMessage);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _monitoringCts?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Represents an update for a dial during monitoring.
    /// </summary>
    public class DialSensorUpdate
    {
        /// <summary>Dial unique ID</summary>
        public string DialUid { get; set; } = string.Empty;

        /// <summary>Friendly display name for the dial</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>HWInfo64 sensor name</summary>
        public string SensorName { get; set; } = string.Empty;

        /// <summary>HWInfo64 entry name</summary>
        public string EntryName { get; set; } = string.Empty;

        /// <summary>Current sensor value</summary>
        public double SensorValue { get; set; }

        /// <summary>Sensor unit of measurement</summary>
        public string SensorUnit { get; set; } = string.Empty;

        /// <summary>Dial percentage (0-100)</summary>
        public byte DialPercentage { get; set; }

        /// <summary>Current backlight color</summary>
        public string CurrentColor { get; set; } = string.Empty;

        /// <summary>Whether sensor is in warning range</summary>
        public bool IsWarning { get; set; }

        /// <summary>Whether sensor is in critical range</summary>
        public bool IsCritical { get; set; }

        /// <summary>Last update timestamp</summary>
        public DateTime LastUpdate { get; set; }

        /// <summary>Total number of successful updates</summary>
        public int UpdateCount { get; set; }

        /// <summary>Gets a formatted tooltip string for display.</summary>
        public string GetTooltip()
        {
            var lines = new List<string>
            {
                $"{DisplayName}",
                $"Sensor: {EntryName}",
                $"Value: {SensorValue:F2} {SensorUnit}",
                $"Dial: {DialPercentage}%",
                $"Color: {CurrentColor}"
            };

            if (IsWarning)
                lines.Add("Status: ? Warning");
            if (IsCritical)
                lines.Add("Status: ? Critical");

            lines.Add($"Updates: {UpdateCount}");
            lines.Add($"Last: {LastUpdate:HH:mm:ss}");

            return string.Join(Environment.NewLine, lines);
        }
    }
}
