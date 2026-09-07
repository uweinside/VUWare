using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VUWare.App.Models;
using VUWare.HWInfo64;
using VUWare.Lib;
using VUWare.Lib.Sensors;

namespace VUWare.App.Services
{
    /// <summary>
    /// Manages real-time monitoring of sensors and updates VU1 dials.
    /// Runs on a background thread and periodically polls sensor data, applies thresholds,
    /// and updates dial positions and colors based on sensor values.
    /// </summary>
    public class SensorMonitoringService : IDisposable
    {
        private readonly VU1Controller _vuController;
        private readonly IDialMappingService _mappingService;
        private DialsConfiguration _config;
        private readonly Dictionary<string, DialMonitoringState> _dialStates;
        private readonly Dictionary<string, ISensorReading> _lastKnownReadings = new();
        private readonly object _configLock = new object();
        private CancellationTokenSource? _monitoringCts;
        private Task? _monitoringTask;
        private bool _disposed;
        private bool _isMonitoring;
        private bool _enableUIUpdates = true;

        /// <summary>
        /// Tracks the state of a single dial during monitoring.
        /// </summary>
        private class DialMonitoringState
        {
            public string DialUid { get; set; } = string.Empty;
            public DialConfig Config { get; set; } = null!;
            public ISensorReading? LastReading { get; set; }
            public byte LastPercentage { get; set; }
            public string LastColor { get; set; } = string.Empty;
            public DateTime LastUpdate { get; set; }
            public int UpdateCount { get; set; }
            public DateTime LastPhysicalUpdate { get; set; } = DateTime.MinValue;
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

        /// <summary>
        /// Creates a new SensorMonitoringService using IDialMappingService abstraction.
        /// This is the preferred constructor for provider-agnostic monitoring.
        /// </summary>
        /// <param name="vuController">VU1 dial controller</param>
        /// <param name="mappingService">Dial mapping service (HWInfo64, OHM, etc.)</param>
        /// <param name="config">Dial configuration</param>
        public SensorMonitoringService(VU1Controller vuController, IDialMappingService mappingService, DialsConfiguration config)
        {
            _vuController = vuController ?? throw new ArgumentNullException(nameof(vuController));
            _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _dialStates = new Dictionary<string, DialMonitoringState>();
        }

        /// <summary>
        /// Creates a new SensorMonitoringService with HWInfo64Controller for backward compatibility.
        /// This constructor supports dial mapping features specific to HWInfo64.
        /// </summary>
        /// <param name="vuController">VU1 dial controller</param>
        /// <param name="hwInfoController">HWInfo64 controller (for dial mapping support)</param>
        /// <param name="config">Dial configuration</param>
        [Obsolete("Use the IDialMappingService constructor for provider-agnostic monitoring")]
        public SensorMonitoringService(VU1Controller vuController, HWInfo64Controller hwInfoController, DialsConfiguration config)
        {
            _vuController = vuController ?? throw new ArgumentNullException(nameof(vuController));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _dialStates = new Dictionary<string, DialMonitoringState>();
            
            // Create a mapping service from the HWInfo64 controller's sensor provider
            _mappingService = new DialMappingService(hwInfoController.SensorProvider);
            
            // Copy existing mappings from HWInfo64Controller to the mapping service
            var existingMappings = hwInfoController.GetAllMappings();
            foreach (var mapping in existingMappings.Values)
            {
                _mappingService.RegisterMapping(new DialSensorMapping
                {
                    Id = mapping.Id,
                    SensorName = mapping.SensorName,
                    SensorId = mapping.SensorId,
                    SensorInstance = mapping.SensorInstance,
                    EntryName = mapping.EntryName,
                    EntryId = mapping.EntryId,
                    MinValue = mapping.MinValue,
                    MaxValue = mapping.MaxValue,
                    WarningThreshold = mapping.WarningThreshold,
                    CriticalThreshold = mapping.CriticalThreshold,
                    DisplayName = mapping.DisplayName
                });
            }
        }

        /// <summary>
        /// Starts the monitoring loop on a background thread.
        /// Uses dedicated thread with high priority for responsiveness under 100% CPU load.
        /// </summary>
        public void Start()
        {
            if (_isMonitoring)
                return;

            // Get active dials based on effective dial count
            var activeDials = _config.GetActiveDials();
            
            System.Diagnostics.Debug.WriteLine($"[SensorMonitoring] Starting monitoring for {activeDials.Count} active dials");

            // Initialize monitoring states for all enabled active dials
            _dialStates.Clear();
            foreach (var dialConfig in activeDials)
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
                    
                    System.Diagnostics.Debug.WriteLine($"[SensorMonitoring] Added {dialConfig.DisplayName} to monitoring");
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
                
                await Task.Delay(500, cancellationToken);

                foreach (var state in _dialStates.Values)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        // Get initial sensor reading using the mapping service
                        var reading = _mappingService.GetReading(state.DialUid);
                        if (reading == null)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"InitializeDials: No initial reading for {state.Config.DisplayName}");
                            continue;
                        }

                        // Calculate percentage
                        byte percentage = CalculatePercentage(reading.Value, state.Config.MinValue, state.Config.MaxValue);
                        
                        state.LastReading = reading;
                        state.LastPercentage = percentage;

                        // Determine color based on thresholds
                        string initialColor = state.Config.GetColorForValue(reading.Value);
                        state.LastColor = initialColor;

                        // Send initial position
                        bool positionSuccess = await _vuController.SetDialPercentageAsync(
                            state.DialUid, percentage);
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
                            $"InitializeDials: {state.Config.DisplayName} initialized ? {percentage}% ({initialColor})");

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
        /// Calculates percentage from sensor value and configured range.
        /// </summary>
        private static byte CalculatePercentage(double value, double minValue, double maxValue)
        {
            double range = maxValue - minValue;
            if (range <= 0) return 0;
            
            double normalized = Math.Clamp((value - minValue) / range, 0.0, 1.0);
            return (byte)(normalized * 100);
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
                        int attemptsCount = 0;
                        int skippedCount = 0;
                        int failedCount = 0;

                        // Process each enabled dial
                        foreach (var state in _dialStates.Values)
                        {
                            if (cancellationToken.IsCancellationRequested)
                                break;

                            try
                            {
                                attemptsCount++;
                                bool updated = await UpdateDialAsync(state, cancellationToken);
                                if (updated)
                                {
                                    anyUpdated = true;
                                }
                                else
                                {
                                    skippedCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                failedCount++;
                                System.Diagnostics.Debug.WriteLine($"[MonitoringLoop] Error updating dial {state.DialUid}: {ex.Message}");
                            }
                        }

                        if (cycle % 10 == 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[MonitoringLoop] Cycle {cycle}: Attempted={attemptsCount}, Updated={anyUpdated}, Skipped={skippedCount}, Failed={failedCount}");
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
                        System.Diagnostics.Debug.WriteLine($"[MonitoringLoop] Cycle error: {ex.Message}");
                        RaiseError($"Monitoring loop error: {ex.Message}");
                        await Task.Delay(1000, cancellationToken);
                    }
                }

                System.Diagnostics.Debug.WriteLine("MonitoringLoop: Stopped");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MonitoringLoop] FATAL: {ex.Message}");
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
            System.Diagnostics.Debug.WriteLine($"[UpdateDial] START for {state.Config.DisplayName}");
            
            // Get current sensor reading using the mapping service
            ISensorReading? reading = _mappingService.GetReading(state.DialUid);
            byte percentage = 0;
            
            if (reading == null)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: No reading from mapping service");
                
                if (_lastKnownReadings.TryGetValue(state.DialUid, out var cachedReading))
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: Using cached reading");
                    reading = cachedReading;
                    percentage = CalculatePercentage(cachedReading.Value, state.Config.MinValue, state.Config.MaxValue);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: No cached data, SKIP");
                    return false;
                }
            }
            else
            {
                _lastKnownReadings[state.DialUid] = reading;
                percentage = CalculatePercentage(reading.Value, state.Config.MinValue, state.Config.MaxValue);
                System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: Got fresh reading {reading.Value:F1}{reading.Unit}");
            }

            // Store previous values BEFORE updating state
            byte previousPercentage = state.LastPercentage;
            string previousColor = state.LastColor;
            
            // Update state with current values
            state.LastReading = reading;
            state.LastPercentage = percentage;
            state.LastColor = state.Config.GetColorForValue(reading.Value);

            // Check if values changed
            bool positionChanged = previousPercentage != state.LastPercentage;
            
            // OPTIMIZATION: In static color mode, never update color after initialization
            bool colorChanged = false;
            if (state.Config.ColorConfig.ColorMode != "static")
            {
                colorChanged = previousColor != state.LastColor;
            }
            
            bool needsUpdate = positionChanged || colorChanged;

            if (!needsUpdate)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: No change (pos={state.LastPercentage}%), SKIP");
                state.LastUpdate = DateTime.Now;
                return false;
            }

            // Debouncing: Check if we're updating too frequently (minimum 100ms between physical updates)
            var timeSinceLastPhysicalUpdate = (DateTime.Now - state.LastPhysicalUpdate).TotalMilliseconds;
            if (timeSinceLastPhysicalUpdate < 100)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: Debouncing ({timeSinceLastPhysicalUpdate:F0}ms), SKIP");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: NEEDS UPDATE - pos={previousPercentage}%?{state.LastPercentage}%, color={previousColor}?{state.LastColor}");

            using var updateCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            updateCts.CancelAfter(TimeSpan.FromSeconds(5));
            
            bool updateSuccess = false;

            // Update dial position
            if (positionChanged)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: Calling SetDialPercentageAsync({state.LastPercentage}%)...");
                bool positionSuccess = false;
                try
                {
                    var positionTask = _vuController.SetDialPercentageAsync(state.DialUid, state.LastPercentage);
                    positionSuccess = await positionTask.WaitAsync(TimeSpan.FromSeconds(2), updateCts.Token);
                    
                    if (!positionSuccess)
                    {
                        System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: ? Position update returned FALSE");
                      }
                      else
                      {
                        System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: ? Position update succeeded");
                        updateSuccess = true;
                      }
                }
                catch (TimeoutException)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: ? Position update TIMEOUT");
                }
                catch (OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: ? Position update CANCELLED");
                    return false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: ? Position update EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                }
            }

            // Update backlight color (only if colorChanged AND not in static mode)
            if (colorChanged && state.Config.ColorConfig.ColorMode != "static")
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: Calling SetBacklightColorAsync({state.LastColor})...");
                var color = GetColorFromName(state.LastColor);
                if (color != null)
                {
                    bool colorSuccess = false;
                    try
                    {
                        var colorTask = _vuController.SetBacklightColorAsync(state.DialUid, color!);
                        colorSuccess = await colorTask.WaitAsync(TimeSpan.FromSeconds(2), updateCts.Token);
                        
                        if (!colorSuccess)
                        {
                            System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: ? Color update returned FALSE");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: ? Color update succeeded");
                            updateSuccess = true;
                        }
                    }
                    catch (TimeoutException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: ? Color update TIMEOUT");
                    }
                    catch (OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: ? Color update CANCELLED");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: ? Color update EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }

            if (!updateSuccess && !positionChanged)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: ? No updates performed");
                return false;
            }

            state.LastUpdate = DateTime.Now;
            state.LastPhysicalUpdate = DateTime.Now;
            state.UpdateCount++;

            System.Diagnostics.Debug.WriteLine($"[UpdateDial] {state.Config.DisplayName}: ? COMPLETE (updateCount={state.UpdateCount})");

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

        /// <summary>
        /// Updates the configuration dynamically without stopping monitoring.
        /// Refreshes dial states with new thresholds, colors, and settings.
        /// </summary>
        /// <param name="newConfig">The new configuration to apply</param>
        public void UpdateConfiguration(DialsConfiguration newConfig)
        {
            if (newConfig == null)
                throw new ArgumentNullException(nameof(newConfig));

            lock (_configLock)
            {
                System.Diagnostics.Debug.WriteLine("[SensorMonitoring] Updating configuration dynamically");

                _config = newConfig;

                // Get active dials based on effective dial count
                var activeDials = newConfig.GetActiveDials();
                
                System.Diagnostics.Debug.WriteLine($"[SensorMonitoring] Active dials after config update: {activeDials.Count}");

                // Update existing dial states with new configuration
                foreach (var dialConfig in activeDials.Where(d => d.Enabled))
                {
                    if (_dialStates.TryGetValue(dialConfig.DialUid, out var state))
                    {
                        // Update the config reference
                        state.Config = dialConfig;
                        System.Diagnostics.Debug.WriteLine($"[SensorMonitoring] Updated config for dial {dialConfig.DisplayName}");
                    }
                    else
                    {
                        // New dial added - add to monitoring
                        _dialStates[dialConfig.DialUid] = new DialMonitoringState
                        {
                            DialUid = dialConfig.DialUid,
                            Config = dialConfig,
                            LastPercentage = 0,
                            LastColor = dialConfig.ColorConfig.NormalColor,
                            LastUpdate = DateTime.Now,
                            UpdateCount = 0
                        };
                        System.Diagnostics.Debug.WriteLine($"[SensorMonitoring] Added new dial {dialConfig.DisplayName} to monitoring");
                    }
                }

                // Remove dials that are no longer active (disabled or beyond effective count)
                var activeDialUids = activeDials.Where(d => d.Enabled).Select(d => d.DialUid).ToHashSet();
                var dialsToRemove = _dialStates.Keys
                    .Where(uid => !activeDialUids.Contains(uid))
                    .ToList();

                foreach (var uid in dialsToRemove)
                {
                    var displayName = _dialStates[uid].Config?.DisplayName ?? uid;
                    _dialStates.Remove(uid);
                    _lastKnownReadings.Remove(uid);
                    System.Diagnostics.Debug.WriteLine($"[SensorMonitoring] Removed dial {displayName} from monitoring");
                }

                System.Diagnostics.Debug.WriteLine($"[SensorMonitoring] Configuration updated - monitoring {_dialStates.Count} dials");
            }
        }

        /// <summary>
        /// Pauses monitoring temporarily without stopping the thread.
        /// Useful for configuration changes that need atomic updates.
        /// </summary>
        public void Pause()
        {
            lock (_configLock)
            {
                _isMonitoring = false;
                System.Diagnostics.Debug.WriteLine("[SensorMonitoring] Monitoring paused");
            }
        }

        /// <summary>
        /// Resumes monitoring after a pause.
        /// </summary>
        public void Resume()
        {
            lock (_configLock)
            {
                _isMonitoring = true;
                System.Diagnostics.Debug.WriteLine("[SensorMonitoring] Monitoring resumed");
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
