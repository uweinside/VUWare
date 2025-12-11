using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using VUWare.App.Models;

namespace VUWare.App.ViewModels
{
    /// <summary>
    /// ViewModel for application-wide settings.
    /// </summary>
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private AppSettings _settings;
        private bool _autoConnect;
        private bool _enablePolling;
        private int _globalUpdateIntervalMs;
        private bool _debugMode;
        private int _serialCommandDelayMs;
        private bool _startMinimized;

        public event PropertyChangedEventHandler? PropertyChanged;

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
            set => SetProperty(ref _globalUpdateIntervalMs, value);
        }

        public bool DebugMode
        {
            get => _debugMode;
            set => SetProperty(ref _debugMode, value);
        }

        public int SerialCommandDelayMs
        {
            get => _serialCommandDelayMs;
            set => SetProperty(ref _serialCommandDelayMs, value);
        }

        public bool StartMinimized
        {
            get => _startMinimized;
            set => SetProperty(ref _startMinimized, value);
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
