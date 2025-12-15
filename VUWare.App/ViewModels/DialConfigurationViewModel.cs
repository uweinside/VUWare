using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using VUWare.App.Models;
using System.Collections;
using System.Collections.Generic;
using VUWare.Lib.Sensors;

namespace VUWare.App.ViewModels
{
    /// <summary>
    /// ViewModel for configuring a single dial.
    /// Implements INotifyPropertyChanged for WPF binding and INotifyDataErrorInfo for validation.
    /// </summary>
    public class DialConfigurationViewModel : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private DialConfig _config;
        private string _displayName;
        private string _sensorName;
        private string _entryName;
        private double _minValue;
        private double _maxValue;
        private double? _warningThreshold;
        private double? _criticalThreshold;
        private string _colorMode;
        private string _staticColor;
        private string _normalColor;
        private string _warningColor;
        private string _criticalColor;
        private int _updateIntervalMs;
        private string _displayFormat;
        private string _displayUnit;
        private bool _enabled;
        private int _decimalPlaces;
        
        // Store the actual sensor ID and instance for unique identification
        private uint _sensorId;
        private uint _sensorInstance;
        
        // Store the actual entry ID for unique identification
        private uint _entryId;

        // Validation error storage
        private readonly Dictionary<string, List<string>> _errors = new Dictionary<string, List<string>>();

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public string DialUid { get; }
        public int DialNumber { get; }

        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (SetProperty(ref _displayName, value))
                {
                    ValidateDisplayName();
                }
            }
        }

        public string SensorName
        {
            get => _sensorName;
            set
            {
                if (SetProperty(ref _sensorName, value))
                {
                    // When sensor name changes, update available entries
                    UpdateAvailableEntries();
                    ValidateSensorName();
                }
            }
        }

        public string EntryName
        {
            get => _entryName;
            set
            {
                if (SetProperty(ref _entryName, value))
                {
                    ValidateEntryName();
                }
            }
        }

        public double MinValue
        {
            get => _minValue;
            set
            {
                if (SetProperty(ref _minValue, value))
                {
                    ValidateMinMaxValues();
                }
            }
        }

        public double MaxValue
        {
            get => _maxValue;
            set
            {
                if (SetProperty(ref _maxValue, value))
                {
                    ValidateMinMaxValues();
                }
            }
        }

        public double? WarningThreshold
        {
            get => _warningThreshold;
            set
            {
                if (SetProperty(ref _warningThreshold, value))
                {
                    ValidateThresholds();
                }
            }
        }

        public double? CriticalThreshold
        {
            get => _criticalThreshold;
            set
            {
                if (SetProperty(ref _criticalThreshold, value))
                {
                    ValidateThresholds();
                }
            }
        }

        public string ColorMode
        {
            get => _colorMode;
            set => SetProperty(ref _colorMode, value);
        }

        public string StaticColor
        {
            get => _staticColor;
            set => SetProperty(ref _staticColor, value);
        }

        public string NormalColor
        {
            get => _normalColor;
            set => SetProperty(ref _normalColor, value);
        }

        public string WarningColor
        {
            get => _warningColor;
            set => SetProperty(ref _warningColor, value);
        }

        public string CriticalColor
        {
            get => _criticalColor;
            set => SetProperty(ref _criticalColor, value);
        }

        public int UpdateIntervalMs
        {
            get => _updateIntervalMs;
            set
            {
                if (SetProperty(ref _updateIntervalMs, value))
                {
                    ValidateUpdateInterval();
                }
            }
        }

        public string DisplayFormat
        {
            get => _displayFormat;
            set => SetProperty(ref _displayFormat, value);
        }

        public string DisplayUnit
        {
            get => _displayUnit;
            set => SetProperty(ref _displayUnit, value);
        }

        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set => SetProperty(ref _decimalPlaces, value);
        }

        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        public ObservableCollection<string> AvailableColors { get; }
        public ObservableCollection<string> ColorModes { get; }
        public ObservableCollection<string> DisplayFormats { get; }
        public ObservableCollection<string> AvailableSensors { get; }
        public ObservableCollection<string> AvailableEntries { get; }
        public ObservableCollection<string> AvailableDisplayUnits { get; }

        // Store all sensor data for filtering entries
        private System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> _sensorEntryMap;
        
        // NEW: Store mapping from display name to sensor metadata (for unique identification)
        private System.Collections.Generic.Dictionary<string, (string OriginalName, uint SensorId, uint SensorInstance)> _displayToSensorMetadata;
        
        // NEW: Store mapping from entry display name to original entry name and metadata
        private System.Collections.Generic.Dictionary<string, (string OriginalName, uint EntryId)> _displayToEntryMetadata;

        // INotifyDataErrorInfo implementation
        public bool HasErrors => _errors.Count > 0;

        public IEnumerable GetErrors(string? propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                return _errors.Values.SelectMany(e => e);
            }

            if (_errors.ContainsKey(propertyName))
            {
                return _errors[propertyName];
            }

            return Enumerable.Empty<string>();
        }

        private void AddError(string propertyName, string error)
        {
            if (!_errors.ContainsKey(propertyName))
            {
                _errors[propertyName] = new List<string>();
            }

            if (!_errors[propertyName].Contains(error))
            {
                _errors[propertyName].Add(error);
                OnErrorsChanged(propertyName);
            }
        }

        private void ClearErrors(string propertyName)
        {
            if (_errors.ContainsKey(propertyName))
            {
                _errors.Remove(propertyName);
                OnErrorsChanged(propertyName);
            }
        }

        private void OnErrorsChanged(string propertyName)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        // Validation methods
        private void ValidateDisplayName()
        {
            ClearErrors(nameof(DisplayName));

            if (string.IsNullOrWhiteSpace(_displayName))
            {
                AddError(nameof(DisplayName), "Display name is required");
            }
        }

        private void ValidateSensorName()
        {
            ClearErrors(nameof(SensorName));

            if (_enabled && string.IsNullOrWhiteSpace(_sensorName))
            {
                AddError(nameof(SensorName), "Sensor name is required when dial is enabled");
            }
        }

        private void ValidateEntryName()
        {
            ClearErrors(nameof(EntryName));

            if (_enabled && string.IsNullOrWhiteSpace(_entryName))
            {
                AddError(nameof(EntryName), "Entry name is required when dial is enabled");
            }
        }

        private void ValidateMinMaxValues()
        {
            ClearErrors(nameof(MinValue));
            ClearErrors(nameof(MaxValue));

            if (_maxValue <= _minValue)
            {
                AddError(nameof(MaxValue), "Max value must be greater than min value");
            }

            // Also re-validate thresholds since they depend on min/max
            ValidateThresholds();
        }

        private void ValidateThresholds()
        {
            ClearErrors(nameof(WarningThreshold));
            ClearErrors(nameof(CriticalThreshold));

            if (_warningThreshold.HasValue && _criticalThreshold.HasValue)
            {
                if (_criticalThreshold.Value <= _warningThreshold.Value)
                {
                    AddError(nameof(CriticalThreshold), "Critical threshold must be greater than warning threshold");
                }
            }
        }

        private void ValidateUpdateInterval()
        {
            ClearErrors(nameof(UpdateIntervalMs));

            if (_updateIntervalMs < 100)
            {
                AddError(nameof(UpdateIntervalMs), "Update interval must be at least 100ms");
            }
        }

        public DialConfigurationViewModel(DialConfig config, int dialNumber)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            DialUid = config.DialUid;
            DialNumber = dialNumber;

            // Initialize properties from config
            _displayName = config.DisplayName;
            _sensorName = config.SensorName;
            _sensorId = config.SensorId;
            _sensorInstance = config.SensorInstance;
            _entryName = config.EntryName;
            _entryId = config.EntryId;
            _minValue = config.MinValue;
            _maxValue = config.MaxValue;
            _warningThreshold = config.WarningThreshold;
            _criticalThreshold = config.CriticalThreshold;
            _colorMode = config.ColorConfig.ColorMode;
            _staticColor = config.ColorConfig.StaticColor;
            _normalColor = config.ColorConfig.NormalColor;
            _warningColor = config.ColorConfig.WarningColor;
            _criticalColor = config.ColorConfig.CriticalColor;
            _updateIntervalMs = config.UpdateIntervalMs;
            _displayFormat = config.DisplayFormat;
            _displayUnit = config.DisplayUnit;
            _enabled = config.Enabled;
            _decimalPlaces = config.DecimalPlaces;

            // Initialize available options
            AvailableColors = new ObservableCollection<string>
            {
                "Red", "Green", "Blue", "Yellow", "Cyan", "Magenta", 
                "Orange", "Purple", "Pink", "White", "Off"
            };

            ColorModes = new ObservableCollection<string>
            {
                "threshold", "static", "off"
            };

            DisplayFormats = new ObservableCollection<string>
            {
                "percentage", "value"
            };

            AvailableDisplayUnits = new ObservableCollection<string>
            {
                "°C",      // Celsius (temperature)
                "°F",      // Fahrenheit (temperature)
                "W",       // Watts (power)
                "V",       // Volts (voltage)
                "mV",      // Millivolts (voltage)
                "A",       // Amperes (current)
                "MHz",     // Megahertz (frequency)
                "GHz",     // Gigahertz (frequency)
                "RPM",     // Revolutions per minute (fan speed)
                "MB/s",    // Megabytes per second (transfer rate)
                "GB/s",    // Gigabytes per second (transfer rate)
                "%",       // Percentage
                "MB",      // Megabytes (memory)
                "GB",      // Gigabytes (memory)
                ""         // Empty option for no unit
            };

            AvailableSensors = new ObservableCollection<string>();
            AvailableEntries = new ObservableCollection<string>();
            _sensorEntryMap = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
            _displayToSensorMetadata = new System.Collections.Generic.Dictionary<string, (string, uint, uint)>();
            _displayToEntryMetadata = new System.Collections.Generic.Dictionary<string, (string, uint)>();

            // Perform initial validation
            ValidateAllProperties();
        }

        /// <summary>
        /// Validates all properties at once (used during initialization)
        /// </summary>
        private void ValidateAllProperties()
        {
            ValidateDisplayName();
            ValidateSensorName();
            ValidateEntryName();
            ValidateMinMaxValues();
            ValidateThresholds();
            ValidateUpdateInterval();
        }

        /// <summary>
        /// Loads sensor data from ISensorReading collection (provider-agnostic).
        /// </summary>
        public void LoadSensorData(IReadOnlyList<ISensorReading> readings)
        {
            System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] LoadSensorData called with {readings?.Count ?? 0} readings");
            
            _sensorEntryMap.Clear();
            AvailableSensors.Clear();
            _displayToSensorMetadata.Clear();
            _displayToEntryMetadata.Clear();

            if (readings == null || readings.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] No readings provided");
                return;
            }

            // Store the original configured values before any updates
            var configuredSensorName = _sensorName;
            var configuredSensorId = _sensorId;
            var configuredSensorInstance = _sensorInstance;
            var configuredEntryName = _entryName;
            var configuredEntryId = _entryId;

            // Group readings by composite key (SensorName + SensorId + SensorInstance)
            // This ensures sensors with duplicate names but different IDs are shown separately
            var groupedBySensor = readings
                .GroupBy(r => new { r.SensorName, r.SensorId, r.SensorInstance })
                .OrderBy(g => g.Key.SensorName)
                .ThenBy(g => g.Key.SensorInstance);

            string? matchingSensorDisplayName = null;
            string? matchingEntryDisplayName = null;
            
            foreach (var sensorGroup in groupedBySensor)
            {
                // Create a unique display name that includes instance if there are duplicates
                var baseName = sensorGroup.Key.SensorName;
                var sensorId = sensorGroup.Key.SensorId;
                var instance = sensorGroup.Key.SensorInstance;
                
                // Check if there are other sensors with the same base name
                var hasDuplicates = groupedBySensor.Count(g => g.Key.SensorName == baseName) > 1;
                
                // If duplicates exist, append instance/ID to make them distinguishable
                var displayName = hasDuplicates && instance > 0
                    ? $"{baseName} (#{instance})"
                    : baseName;
                
                AvailableSensors.Add(displayName);

                // Build entries with disambiguation
                var entryList = sensorGroup.ToList();
                var entryGroups = entryList.GroupBy(r => r.EntryName);
                var disambiguatedEntries = new System.Collections.Generic.List<string>();

                foreach (var entryGroup in entryGroups.OrderBy(g => g.Key))
                {
                    var entryName = entryGroup.Key;
                    var entryReadings = entryGroup.ToList();

                    if (entryReadings.Count > 1)
                    {
                        // Multiple entries with the same name - add disambiguation
                        for (int i = 0; i < entryReadings.Count; i++)
                        {
                            var reading = entryReadings[i];
                            // Use entry type or index for disambiguation
                            var disambiguatedName = $"{entryName} ({reading.Category})";
                            disambiguatedEntries.Add(disambiguatedName);
                            // Use composite key for entry metadata to avoid collisions across sensors
                            var entryKey = $"{displayName}|{disambiguatedName}";
                            _displayToEntryMetadata[entryKey] = (entryName, reading.EntryId);
                        }
                    }
                    else
                    {
                        // Single entry with this name - no disambiguation needed
                        var reading = entryReadings[0];
                        disambiguatedEntries.Add(entryName);
                        var entryKey = $"{displayName}|{entryName}";
                        _displayToEntryMetadata[entryKey] = (entryName, reading.EntryId);
                    }
                }

                _sensorEntryMap[displayName] = disambiguatedEntries;
                _displayToSensorMetadata[displayName] = (baseName, sensorId, instance);
                
                // Check if this sensor matches the currently configured sensor
                // Match by name AND (if set) by ID/instance for precision
                bool isSensorMatch = false;
                if (configuredSensorId == 0 && configuredSensorInstance == 0)
                {
                    // Old config without ID/instance - match by name only
                    isSensorMatch = baseName.Equals(configuredSensorName, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    // New config with ID/instance - match by composite key
                    isSensorMatch = baseName.Equals(configuredSensorName, StringComparison.OrdinalIgnoreCase) &&
                                   sensorId == configuredSensorId &&
                                   instance == configuredSensorInstance;
                }
                
                if (isSensorMatch)
                {
                    matchingSensorDisplayName = displayName;
                    _sensorId = sensorId;  // Update with actual values if upgrading from old config
                    _sensorInstance = instance;
                    System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Found matching sensor '{configuredSensorName}' -> display name '{displayName}'");
                    
                    // Now find the matching entry within this sensor
                    foreach (var entryDisplayName in disambiguatedEntries)
                    {
                        var entryKey = $"{displayName}|{entryDisplayName}";
                        if (_displayToEntryMetadata.TryGetValue(entryKey, out var meta))
                        {
                            // Match by EntryId first (most precise)
                            if (configuredEntryId != 0 && meta.EntryId == configuredEntryId)
                            {
                                matchingEntryDisplayName = entryDisplayName;
                                System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Found matching entry by ID {configuredEntryId} -> '{entryDisplayName}'");
                                break;
                            }
                            // Match by original entry name
                            else if (meta.OriginalName.Equals(configuredEntryName, StringComparison.OrdinalIgnoreCase))
                            {
                                matchingEntryDisplayName = entryDisplayName;
                                _entryId = meta.EntryId;
                                System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Found matching entry by name '{configuredEntryName}' -> '{entryDisplayName}'");
                                // Don't break - keep looking for EntryId match
                            }
                        }
                    }
                }
            }

            // Update to display names for UI binding
            if (matchingSensorDisplayName != null)
            {
                _sensorName = matchingSensorDisplayName;
                System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Set sensor to display name: '{_sensorName}'");
            }
            
            if (matchingEntryDisplayName != null)
            {
                _entryName = matchingEntryDisplayName;
                System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Set entry to display name: '{_entryName}'");
            }

            System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Final SensorName = '{_sensorName}', EntryName = '{_entryName}'");

            // Update available entries for current sensor (AFTER updating _entryName)
            UpdateAvailableEntriesWithoutAutoSelect();
            
            // Notify UI
            OnPropertyChanged(nameof(SensorName));
            OnPropertyChanged(nameof(EntryName));
        }

        /// <summary>
        /// Loads sensor data from HWInfo64.SensorReading list (backward compatibility).
        /// </summary>
        public void LoadSensorData(System.Collections.Generic.List<VUWare.HWInfo64.SensorReading> readings)
        {
            // HWInfo64.SensorReading implements ISensorReading, so we can cast directly
            if (readings == null || readings.Count == 0)
            {
                LoadSensorData(Array.Empty<ISensorReading>());
                return;
            }
            
            LoadSensorData(readings.Cast<ISensorReading>().ToList());
        }

        /// <summary>
        /// Updates the available entries based on the currently selected sensor.
        /// Does NOT auto-select - used when loading data to preserve configured entry.
        /// </summary>
        private void UpdateAvailableEntriesWithoutAutoSelect()
        {
            AvailableEntries.Clear();

            if (!string.IsNullOrWhiteSpace(_sensorName) && _sensorEntryMap.ContainsKey(_sensorName))
            {
                foreach (var entry in _sensorEntryMap[_sensorName])
                {
                    AvailableEntries.Add(entry);
                }
            }

            OnPropertyChanged(nameof(AvailableEntries));
        }

        /// <summary>
        /// Updates the available entries based on the currently selected sensor.
        /// Auto-selects first entry if current entry is invalid (used when user changes sensor).
        /// </summary>
        private void UpdateAvailableEntries()
        {
            AvailableEntries.Clear();

            if (!string.IsNullOrWhiteSpace(_sensorName) && _sensorEntryMap.ContainsKey(_sensorName))
            {
                foreach (var entry in _sensorEntryMap[_sensorName])
                {
                    AvailableEntries.Add(entry);
                }
                
                // Check if current entry name is valid (exists in the list)
                bool currentEntryIsValid = !string.IsNullOrWhiteSpace(_entryName) && 
                                          AvailableEntries.Contains(_entryName);
                
                // Auto-select the first entry only if no valid entry is currently selected
                if (AvailableEntries.Count > 0 && !currentEntryIsValid)
                {
                    EntryName = AvailableEntries[0];
                    System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Auto-selected first entry: '{EntryName}'");
                }
                else if (currentEntryIsValid)
                {
                    System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Current entry '{_entryName}' is valid, keeping it");
                }
            }
            else
            {
                // Clear entry name if no sensor selected or sensor not found
                if (!string.IsNullOrWhiteSpace(_entryName))
                {
                    EntryName = string.Empty;
                }
            }

            OnPropertyChanged(nameof(AvailableEntries));
        }

        /// <summary>
        /// Applies the view model changes back to the original DialConfig object.
        /// </summary>
        public void ApplyChanges()
        {
            _config.DisplayName = _displayName;
            
            // Convert display name back to original sensor name for HWInfo lookup
            if (_displayToSensorMetadata.TryGetValue(_sensorName, out var sensorMetadata))
            {
                _config.SensorName = sensorMetadata.OriginalName;
                _config.SensorId = sensorMetadata.SensorId;
                _config.SensorInstance = sensorMetadata.SensorInstance;
                System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Converted sensor '{_sensorName}' to '{sensorMetadata.OriginalName}' (ID:{sensorMetadata.SensorId}, Inst:{sensorMetadata.SensorInstance})");
            }
            else
            {
                _config.SensorName = _sensorName;
            }
            
            // Convert entry display name back to original entry name
            // Use composite key: "sensorDisplayName|entryDisplayName"
            var entryKey = $"{_sensorName}|{_entryName}";
            if (_displayToEntryMetadata.TryGetValue(entryKey, out var entryMetadata))
            {
                _config.EntryName = entryMetadata.OriginalName;
                _config.EntryId = entryMetadata.EntryId;
                System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Converted entry '{_entryName}' to '{entryMetadata.OriginalName}' (ID:{entryMetadata.EntryId})");
            }
            else
            {
                _config.EntryName = _entryName;
            }
            
            _config.MinValue = _minValue;
            _config.MaxValue = _maxValue;
            _config.WarningThreshold = _warningThreshold;
            _config.CriticalThreshold = _criticalThreshold;
            _config.ColorConfig.ColorMode = _colorMode;
            _config.ColorConfig.StaticColor = _staticColor;
            _config.ColorConfig.NormalColor = _normalColor;
            _config.ColorConfig.WarningColor = _warningColor;
            _config.ColorConfig.CriticalColor = _criticalColor;
            _config.UpdateIntervalMs = _updateIntervalMs;
            _config.DisplayFormat = _displayFormat;
            _config.DisplayUnit = _displayUnit;
            _config.Enabled = _enabled;
            _config.DecimalPlaces = _decimalPlaces;
        }

        /// <summary>
        /// Resets the view model to match the original DialConfig values.
        /// </summary>
        public void DiscardChanges()
        {
            _displayName = _config.DisplayName;
            _sensorName = _config.SensorName;
            _entryName = _config.EntryName;
            _minValue = _config.MinValue;
            _maxValue = _config.MaxValue;
            _warningThreshold = _config.WarningThreshold;
            _criticalThreshold = _config.CriticalThreshold;
            _colorMode = _config.ColorConfig.ColorMode;
            _staticColor = _config.ColorConfig.StaticColor;
            _normalColor = _config.ColorConfig.NormalColor;
            _warningColor = _config.ColorConfig.WarningColor;
            _criticalColor = _config.ColorConfig.CriticalColor;
            _updateIntervalMs = _config.UpdateIntervalMs;
            _displayFormat = _config.DisplayFormat;
            _displayUnit = _config.DisplayUnit;
            _enabled = _config.Enabled;
            _decimalPlaces = _config.DecimalPlaces;

            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(SensorName));
            OnPropertyChanged(nameof(EntryName));
            OnPropertyChanged(nameof(MinValue));
            OnPropertyChanged(nameof(MaxValue));
            OnPropertyChanged(nameof(WarningThreshold));
            OnPropertyChanged(nameof(CriticalThreshold));
            OnPropertyChanged(nameof(ColorMode));
            OnPropertyChanged(nameof(StaticColor));
            OnPropertyChanged(nameof(NormalColor));
            OnPropertyChanged(nameof(WarningColor));
            OnPropertyChanged(nameof(CriticalColor));
            OnPropertyChanged(nameof(UpdateIntervalMs));
            OnPropertyChanged(nameof(DisplayFormat));
            OnPropertyChanged(nameof(DisplayUnit));
            OnPropertyChanged(nameof(Enabled));
            OnPropertyChanged(nameof(DecimalPlaces));
        }

        protected bool SetProperty<T>(ref T backingField, T value, [CallerMemberName] string propertyName = "")
        {
            if (!Equals(backingField, value))
            {
                backingField = value;
                OnPropertyChanged(propertyName);
                return true;
            }
            return false;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
