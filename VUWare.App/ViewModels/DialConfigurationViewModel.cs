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

        // Store all sensor data for filtering entries
        private System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> _sensorEntryMap;

        public DialConfigurationViewModel(DialConfig config, int dialNumber)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            DialUid = config.DialUid;
            DialNumber = dialNumber;

            // Initialize properties from config
            _displayName = config.DisplayName;
            _sensorName = config.SensorName;
            _entryName = config.EntryName;
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

            AvailableSensors = new ObservableCollection<string>();
            AvailableEntries = new ObservableCollection<string>();
            _sensorEntryMap = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
        }

        /// <summary>
        /// Loads sensor data from HWInfo64 readings.
        /// </summary>
        public void LoadSensorData(System.Collections.Generic.List<VUWare.HWInfo64.SensorReading> readings)
        {
            System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] LoadSensorData called with {readings?.Count ?? 0} readings");
            
            _sensorEntryMap.Clear();
            AvailableSensors.Clear();

            if (readings == null || readings.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] No readings provided");
                return;
            }

            // Group readings by sensor name
            var groupedBySensor = readings.GroupBy(r => r.SensorName).OrderBy(g => g.Key);

            int sensorCount = 0;
            foreach (var sensorGroup in groupedBySensor)
            {
                var sensorName = sensorGroup.Key;
                AvailableSensors.Add(sensorName);
                sensorCount++;

                // Store all entries for this sensor
                var entries = sensorGroup.Select(r => r.EntryName).Distinct().OrderBy(e => e).ToList();
                _sensorEntryMap[sensorName] = entries;
            }

            System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Loaded {sensorCount} sensors");
            System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] AvailableSensors.Count = {AvailableSensors.Count}");
            System.Diagnostics.Debug.WriteLine($"[DialVM {DialNumber}] Current SensorName = '{_sensorName}'");
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
            _config.SensorName = _sensorName;
            _config.EntryName = _entryName;
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
