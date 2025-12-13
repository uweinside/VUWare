using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using VUWare.App.Models;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace VUWare.App.ViewModels
{
    /// <summary>
    /// ViewModel for application-wide settings.
    /// Implements INotifyPropertyChanged and INotifyDataErrorInfo for validation.
    /// </summary>
    public class SettingsViewModel : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private AppSettings _settings;
        private bool _autoConnect;
        private bool _enablePolling;
        private int _globalUpdateIntervalMs;
        private bool _debugMode;
        private int _serialCommandDelayMs;
        private bool _startMinimized;

        // Validation error storage
        private readonly Dictionary<string, List<string>> _errors = new Dictionary<string, List<string>>();

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

        public bool AutoConnect
        {
            get => _autoConnect;
            set => SetProperty(ref _autoConnect, value);
        }

        public bool EnablePolling
        {
            get => _enablePolling;
            set => SetProperty(ref _enablePolling, value);
        }

        public int GlobalUpdateIntervalMs
        {
            get => _globalUpdateIntervalMs;
            set
            {
                if (SetProperty(ref _globalUpdateIntervalMs, value))
                {
                    ValidateGlobalUpdateInterval();
                }
            }
        }

        public bool DebugMode
        {
            get => _debugMode;
            set => SetProperty(ref _debugMode, value);
        }

        public int SerialCommandDelayMs
        {
            get => _serialCommandDelayMs;
            set
            {
                if (SetProperty(ref _serialCommandDelayMs, value))
                {
                    ValidateSerialCommandDelay();
                }
            }
        }

        public bool StartMinimized
        {
            get => _startMinimized;
            set => SetProperty(ref _startMinimized, value);
        }

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
        private void ValidateGlobalUpdateInterval()
        {
            ClearErrors(nameof(GlobalUpdateIntervalMs));

            if (_globalUpdateIntervalMs < 100)
            {
                AddError(nameof(GlobalUpdateIntervalMs), "Global update interval must be at least 100ms");
            }
            else if (_globalUpdateIntervalMs > 60000)
            {
                AddError(nameof(GlobalUpdateIntervalMs), "Global update interval should not exceed 60000ms (60 seconds)");
            }
        }

        private void ValidateSerialCommandDelay()
        {
            ClearErrors(nameof(SerialCommandDelayMs));

            if (_serialCommandDelayMs < 0)
            {
                AddError(nameof(SerialCommandDelayMs), "Serial command delay cannot be negative");
            }
            else if (_serialCommandDelayMs > 1000)
            {
                AddError(nameof(SerialCommandDelayMs), "Serial command delay should not exceed 1000ms");
            }
        }

        public SettingsViewModel(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // Initialize properties from settings
            _autoConnect = settings.AutoConnect;
            _enablePolling = settings.EnablePolling;
            _globalUpdateIntervalMs = settings.GlobalUpdateIntervalMs;
            _debugMode = settings.DebugMode;
            _serialCommandDelayMs = settings.SerialCommandDelayMs;
            _startMinimized = settings.StartMinimized;

            // Perform initial validation
            ValidateAllProperties();
        }

        /// <summary>
        /// Validates all properties at once (used during initialization)
        /// </summary>
        private void ValidateAllProperties()
        {
            ValidateGlobalUpdateInterval();
            ValidateSerialCommandDelay();
        }

        /// <summary>
        /// Applies the view model changes back to the original AppSettings object.
        /// </summary>
        public void ApplyChanges()
        {
            _settings.AutoConnect = _autoConnect;
            _settings.EnablePolling = _enablePolling;
            _settings.GlobalUpdateIntervalMs = _globalUpdateIntervalMs;
            _settings.DebugMode = _debugMode;
            _settings.SerialCommandDelayMs = _serialCommandDelayMs;
            _settings.StartMinimized = _startMinimized;
        }

        /// <summary>
        /// Resets the view model to match the original AppSettings values.
        /// </summary>
        public void DiscardChanges()
        {
            _autoConnect = _settings.AutoConnect;
            _enablePolling = _settings.EnablePolling;
            _globalUpdateIntervalMs = _settings.GlobalUpdateIntervalMs;
            _debugMode = _settings.DebugMode;
            _serialCommandDelayMs = _settings.SerialCommandDelayMs;
            _startMinimized = _settings.StartMinimized;

            OnPropertyChanged(nameof(AutoConnect));
            OnPropertyChanged(nameof(EnablePolling));
            OnPropertyChanged(nameof(GlobalUpdateIntervalMs));
            OnPropertyChanged(nameof(DebugMode));
            OnPropertyChanged(nameof(SerialCommandDelayMs));
            OnPropertyChanged(nameof(StartMinimized));
            
            // Re-validate after discarding changes
            ValidateAllProperties();
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
