using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        public string SensorName
        {
            get => _sensorName;
            set => SetProperty(ref _sensorName, value);
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

        public DialConfigurationViewModel(DialConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            DialUid = config.DialUid;

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

        protected void SetProperty<T>(ref T backingField, T value, [CallerMemberName] string propertyName = "")
        {
            if (!Equals(backingField, value))
            {
                backingField = value;
                OnPropertyChanged(propertyName);
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
