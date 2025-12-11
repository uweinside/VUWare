using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using VUWare.HWInfo64;

namespace VUWare.App.ViewModels
{
    /// <summary>
    /// Represents a sensor reading for display in the UI.
    /// </summary>
    public class SensorReadingViewModel : INotifyPropertyChanged
    {
        private string _sensorName;
        private string _entryName;
        private double _value;
        private string _unit;
        private string _displayText;

        public event PropertyChangedEventHandler? PropertyChanged;

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

        public double Value
        {
            get => _value;
            set
            {
                SetProperty(ref _value, value);
                UpdateDisplayText();
            }
        }

        public string Unit
        {
            get => _unit;
            set
            {
                SetProperty(ref _unit, value);
                UpdateDisplayText();
            }
        }

        public string DisplayText
        {
            get => _displayText;
            private set => SetProperty(ref _displayText, value);
        }

        public SensorReadingViewModel(SensorReading reading)
        {
            _sensorName = reading.SensorName;
            _entryName = reading.EntryName;
            _value = reading.Value;
            _unit = reading.Unit;
            _displayText = "";
            UpdateDisplayText();
        }

        private void UpdateDisplayText()
        {
            DisplayText = $"{_sensorName} > {_entryName}: {_value:F2} {_unit}";
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

    /// <summary>
    /// ViewModel for browsing available HWInfo64 sensors.
    /// </summary>
    public class AvailableSensorsViewModel : INotifyPropertyChanged
    {
        private string _searchText;
        private SensorReadingViewModel? _selectedSensor;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<SensorReadingViewModel> AllSensors { get; }
        public ObservableCollection<SensorReadingViewModel> FilteredSensors { get; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterSensors();
                }
            }
        }

        public SensorReadingViewModel? SelectedSensor
        {
            get => _selectedSensor;
            set => SetProperty(ref _selectedSensor, value);
        }

        public AvailableSensorsViewModel()
        {
            AllSensors = new ObservableCollection<SensorReadingViewModel>();
            FilteredSensors = new ObservableCollection<SensorReadingViewModel>();
            _searchText = string.Empty;
        }

        /// <summary>
        /// Loads sensors from HWInfo64Controller.
        /// </summary>
        public void LoadSensors(HWInfo64Controller controller)
        {
            if (controller == null || !controller.IsConnected)
                return;

            AllSensors.Clear();
            
            var readings = controller.GetAllSensorReadings();
            foreach (var reading in readings.OrderBy(r => r.SensorName).ThenBy(r => r.EntryName))
            {
                AllSensors.Add(new SensorReadingViewModel(reading));
            }

            FilterSensors();
        }

        private void FilterSensors()
        {
            FilteredSensors.Clear();

            if (string.IsNullOrWhiteSpace(_searchText))
            {
                foreach (var sensor in AllSensors)
                {
                    FilteredSensors.Add(sensor);
                }
            }
            else
            {
                var searchLower = _searchText.ToLower();
                foreach (var sensor in AllSensors)
                {
                    if (sensor.SensorName.ToLower().Contains(searchLower) ||
                        sensor.EntryName.ToLower().Contains(searchLower))
                    {
                        FilteredSensors.Add(sensor);
                    }
                }
            }

            OnPropertyChanged(nameof(FilteredSensors));
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
