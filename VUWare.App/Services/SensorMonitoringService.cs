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
        private readonly Dictionary<string, SensorReading> _lastKnownReadings = new();  // Cache for HWInfo64 timeouts
        private CancellationTokenSource? _monitoringCts;
        private Task? _monitoringTask;
        private bool _disposed;
        private bool _isMonitoring;
        private bool _enableUIUpdates = true; // New: Control UI update behavior

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
            public DateTime LastPhysicalUpdate { get; set; } = DateTime.MinValue; // New: Track physical dial update time
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

        /// <summary>
        /// Enables or disables UI updates via Dispatcher.
        /// Set to false when window is minimized to tray to eliminate unnecessary cross-thread marshaling.
        /// Physical dial updates continue regardless of this setting.
        /// </summary>
        public void SetUIUpdateEnabled(bool enabled)
        {
            _enableUIUpdates = enabled;
            System.Diagnostics.Debug.WriteLine($"[SensorMonitoring] UI updates {(enabled ? "ENABLED" : "DISABLED")}");
        }

        public SensorMonitoringService(VU1Controller vuController, HWInfo64Controller hwInfoController, DialsConfiguration config)
        {
            _vuController = vuController ?? throw new ArgumentNullException(nameof(vuController));
            _hwInfoController = hwInfoController ?? throw new ArgumentNullException(nameof(hwInfoController));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _dialStates = new Dictionary<string, DialMonitoringState>();
        }

        /// <summary>
        /// Starts the monitoring loop on a background thread.
        /// Uses dedicated thread with high priority for responsiveness under 100% CPU load.
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
            
            // Use dedicated thread instead of Task.Run to avoid thread pool starvation
            var monitoringThread = new Thread(async () =>
            {
                // Send initial values to all dials before starting the loop
                await InitializeDials(_monitoringCts.Token);
                // Then start the monitoring loop
                MonitoringLoop(_monitoringCts.Token);
            })
            {
                Name = "Sensor Monitoring",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal  // Higher priority for time-critical updates
            };
            
            monitoringThread.Start();
            _monitoringTask = Task.CompletedTask;  // Track that monitoring was started
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
                                state.DialUid, color!);
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
                int cycle = 0;

                while (!cancellationToken.IsCancellationRequested && _isMonitoring)
                {
                    try
                    {
                        cycle++;
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

                        if (cycle % 10 == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"MonitoringLoop: Cycle {cycle}, Updated: {anyUpdated}");
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
        /// Implements debouncing to prevent rapid successive updates.
        /// Uses cached readings when HWInfo64 is blocked under load.
        /// </summary>
        private async Task<bool> UpdateDialAsync(DialMonitoringState state, CancellationToken cancellationToken)
        {
            // Get current sensor reading
            var status = _hwInfoController.GetSensorStatus(state.DialUid);
            
            if (status == null)
            {
                // Try to use cached reading if available
                if (_lastKnownReadings.TryGetValue(state.DialUid, out var cachedReading))
                {
                    // Calculate percentage from cached value
                    double range = state.Config.MaxValue - state.Config.MinValue;
                    double normalized = Math.Clamp((cachedReading.Value - state.Config.MinValue) / range, 0.0, 1.0);
                    byte cachedPercentage = (byte)(normalized * 100);
                    
                    // Use cached data
                    status = new SensorStatus
                    {
                        MappingId = state.DialUid,
                        SensorReading = cachedReading,
                        Percentage = cachedPercentage,
                        IsCritical = state.Config.CriticalThreshold.HasValue && 
                                    cachedReading.Value >= state.Config.CriticalThreshold.Value,
                        IsWarning = state.Config.WarningThreshold.HasValue && 
                                   cachedReading.Value >= state.Config.WarningThreshold.Value
                    };
                }
                else
                {
                    // No cached data and no current data - skip update
                    return false;
                }
            }
            else
            {
                // Update cache with fresh reading
                _lastKnownReadings[state.DialUid] = status.SensorReading!;
            }

            // Store previous values BEFORE updating state
            byte previousPercentage = state.LastPercentage;
            string previousColor = state.LastColor;
            
            // Update state with current values
            state.LastReading = status.SensorReading;
            state.LastPercentage = status.Percentage;
            state.LastColor = state.Config.GetColorForValue(status.SensorReading!.Value);

            // Check if values changed
            bool positionChanged = previousPercentage != state.LastPercentage;
            bool colorChanged = previousColor != state.LastColor;
            bool needsUpdate = positionChanged || colorChanged;

            if (!needsUpdate)
            {
                // Still update the timestamp even if no change
                state.LastUpdate = DateTime.Now;
                return false;
            }

            // Debouncing: Check if we're updating too frequently (minimum 100ms between physical updates)
            var timeSinceLastPhysicalUpdate = (DateTime.Now - state.LastPhysicalUpdate).TotalMilliseconds;
            if (timeSinceLastPhysicalUpdate < 100)
            {
                // Skip this update - too soon after last physical update
                return false;
            }

            // Update dial position
            bool positionSuccess = await _vuController.SetDialPercentageAsync(state.DialUid, state.LastPercentage);
            if (!positionSuccess)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update dial position for {state.DialUid}");
                return false;
            }

            // Update backlight color
            var color = GetColorFromName(state.LastColor);
            if (color != null)
            {
                bool colorSuccess = await _vuController.SetBacklightColorAsync(state.DialUid, color!);
                if (!colorSuccess)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to update dial color for {state.DialUid}");
                    return false;
                }
            }

            // Increment update count and timestamp
            state.LastUpdate = DateTime.Now;
            state.LastPhysicalUpdate = DateTime.Now; // Track physical update time
            state.UpdateCount++;

            System.Diagnostics.Debug.WriteLine(
                $"Dial updated: {state.Config.DisplayName} ? {state.LastPercentage}% ({state.LastColor})" +
                $"{(positionChanged ? " [position]" : "")}{(colorChanged ? " [color]" : "")}");

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
            // Skip UI updates if window is hidden/minimized
            if (!_enableUIUpdates)
            {
                return;
            }

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
