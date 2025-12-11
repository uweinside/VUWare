using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using VUWare.App.Models;

namespace VUWare.App.ViewModels
{
    /// <summary>
    /// ViewModel for configuring a single dial.
    /// Implements INotifyPropertyChanged for WPF binding.
    /// </summary>
    public class DialConfigurationViewModel : INotifyPropertyChanged
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

        public event PropertyChangedEventHandler? PropertyChanged;

        public string DialUid { get; }
        public int DialNumber { get; }

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
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
                }
            }
        }

        public string EntryName
        {
            get => _entryName;
            set => SetProperty(ref _entryName, value);
        }

        public double MinValue
        {
            get => _minValue;
            set => SetProperty(ref _minValue, value);
        }

        public double MaxValue
        {
            get => _maxValue;
            set => SetProperty(ref _maxValue, value);
        }

        public double? WarningThreshold
        {
            get => _warningThreshold;
            set => SetProperty(ref _warningThreshold, value);
        }

        public double? CriticalThreshold
        {
            get => _criticalThreshold;
            set => SetProperty(ref _criticalThreshold, value);
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
            set => SetProperty(ref _updateIntervalMs, value);
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
        }

        /// <summary>
        /// Loads sensor data from HWInfo64 readings.
        /// </summary>
        public void LoadSensorData(System.Collections.Generic.List<VUWare.HWInfo64.SensorReading> readings)
        {
            System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] LoadSensorData called with {readings?.Count ?? 0} readings");
            
            _sensorEntryMap.Clear();
            AvailableSensors.Clear();
            _displayToSensorMetadata.Clear(); // Clear the display name map
            _displayToEntryMetadata.Clear(); // Clear the entry display name map

            if (readings == null || readings.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] No readings provided");
                return;
            }

            // Group readings by composite key (SensorName + SensorId + SensorInstance)
            // This ensures sensors with duplicate names but different IDs are shown separately
            var groupedBySensor = readings
                .GroupBy(r => new { r.SensorName, r.SensorId, r.SensorInstance })
                .OrderBy(g => g.Key.SensorName)
                .ThenBy(g => g.Key.SensorInstance);

            int sensorCount = 0;
            string? matchingDisplayName = null; // Track if current config sensor name matches any sensor
            
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
                sensorCount++;

                // Build entries with disambiguation for duplicates within this sensor
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
                            var disambiguatedName = $"{entryName} ({reading.Type})";
                            disambiguatedEntries.Add(disambiguatedName);
                            _displayToEntryMetadata[disambiguatedName] = (entryName, reading.EntryId);
                            System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}]   Entry: '{disambiguatedName}' -> original '{entryName}' (ID:{reading.EntryId})");
                        }
                    }
                    else
                    {
                        // Single entry with this name - no disambiguation needed
                        var reading = entryReadings[0];
                        disambiguatedEntries.Add(entryName);
                        _displayToEntryMetadata[entryName] = (entryName, reading.EntryId);
                        System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}]   Entry: '{entryName}' (ID:{reading.EntryId})");
                    }
                }

                _sensorEntryMap[displayName] = disambiguatedEntries;
                
                // NEW: Map the display name (with instance) to the sensor metadata
                _displayToSensorMetadata[displayName] = (baseName, sensorId, instance);
                
                System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Added sensor: '{displayName}' (ID:{sensorId}, Inst:{instance}) with {disambiguatedEntries.Count} entries");
            }

            // If the config has an original sensor name, update it to the display name for UI binding
            if (matchingDisplayName != null && matchingDisplayName != _sensorName)
            {
                System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Updating sensor name from '{_sensorName}' to '{matchingDisplayName}' for UI display");
                _sensorName = matchingDisplayName; // Update to display name for UI binding
            }

            System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Loaded {sensorCount} sensors");
            System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] AvailableSensors.Count = {AvailableSensors.Count}");
            System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Current SensorName = '{_sensorName}' (ID:{_sensorId}, Inst:{_sensorInstance})");
            System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Current EntryName = '{_entryName}'");

            // Update available entries for current sensor
            UpdateAvailableEntries();
            
            // Notify UI that sensor and entry names should be re-evaluated
            OnPropertyChanged(nameof(SensorName));
            OnPropertyChanged(nameof(EntryName));
        }

        /// <summary>
        /// Updates the available entries based on the currently selected sensor.
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
                
                // Auto-select the first entry if available and no entry is currently selected or the current entry is not in the list
                if (AvailableEntries.Count > 0)
                {
                    // Only auto-select if current entry is not in the new list
                    if (string.IsNullOrWhiteSpace(_entryName) || !AvailableEntries.Contains(_entryName))
                    {
                        EntryName = AvailableEntries[0];
                        System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Auto-selected first entry: '{EntryName}'");
                    }
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
            
            // IMPORTANT: Convert display name (with instance #) back to original sensor name for HWInfo lookup
            if (_displayToSensorMetadata.TryGetValue(_sensorName, out var sensorMetadata))
            {
                // Use the original sensor name (without instance suffix) for config
                _config.SensorName = sensorMetadata.OriginalName;
                _config.SensorId = sensorMetadata.SensorId;
                _config.SensorInstance = sensorMetadata.SensorInstance;
                System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Converted sensor display name '{_sensorName}' to original '{sensorMetadata.OriginalName}' (ID:{sensorMetadata.SensorId}, Inst:{sensorMetadata.SensorInstance})");
            }
            else
            {
                // No mapping exists (sensor name doesn't have instance suffix)
                _config.SensorName = _sensorName;
            }
            
            // IMPORTANT: Convert entry display name (with disambiguation) back to original entry name
            if (_displayToEntryMetadata.TryGetValue(_entryName, out var entryMetadata))
            {
                // Use the original entry name (without disambiguation) for config
                _config.EntryName = entryMetadata.OriginalName;
                _config.EntryId = entryMetadata.EntryId;
                System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Converted entry display name '{_entryName}' to original '{entryMetadata.OriginalName}' (ID:{entryMetadata.EntryId})");
            }
            else
            {
                // No mapping exists (entry name doesn't have disambiguation)
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
