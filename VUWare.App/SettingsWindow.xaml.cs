using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VUWare.App.Models;
using VUWare.App.Services;
using VUWare.App.ViewModels;
using VUWare.HWInfo64;
using System.IO;

namespace VUWare.App
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private DialsConfiguration _configuration;
        private SettingsViewModel _settingsViewModel;
        private List<DialConfigurationViewModel> _dialViewModels;
        private HWInfo64Controller? _hwInfoController;
        private VUWare.Lib.VU1Controller? _vu1Controller;

        public SettingsWindow()
        {
            InitializeComponent();
            
            // Load configuration
            _configuration = ConfigManager.LoadDefault() ?? ConfigManager.CreateDefault();
            
            // Initialize view models
            _settingsViewModel = new SettingsViewModel(_configuration.AppSettings);
            _dialViewModels = new List<DialConfigurationViewModel>();

            // Initialize after InitializeComponent
            Loaded += SettingsWindow_Loaded;
        }

        /// <summary>
        /// Sets the HWInfo64 controller for sensor browsing.
        /// </summary>
        public void SetHWInfo64Controller(HWInfo64Controller controller)
        {
            System.Diagnostics.Debug.WriteLine($"[SettingsWindow] SetHWInfo64Controller called");
            _hwInfoController = controller;
            
            if (_hwInfoController != null && _hwInfoController.IsConnected)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsWindow] HWInfo controller is connected");
                
                // Load sensor data for all dial view models
                var readings = _hwInfoController.GetAllSensorReadings();
                System.Diagnostics.Debug.WriteLine($"[SettingsWindow] Got {readings?.Count ?? 0} readings from HWInfo");
                System.Diagnostics.Debug.WriteLine($"[SettingsWindow] Loading sensor data for {_dialViewModels.Count} dial view models");
                
                foreach (var dialViewModel in _dialViewModels)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsWindow] Loading data for Dial #{dialViewModel.DialNumber}");
                    dialViewModel.LoadSensorData(readings);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsWindow] HWInfo controller is null or not connected");
            }
        }

        /// <summary>
        /// Sets the VU1 controller for image uploads.
        /// </summary>
        public void SetVU1Controller(VUWare.Lib.VU1Controller controller)
        {
            _vu1Controller = controller;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Set data contexts after window is loaded
            SetDataContexts();
            
            // Initialize dial configuration panels
            InitializeDialPanels();
        }

        /// <summary>
        /// Sets data contexts for General Settings and other controls.
        /// </summary>
        private void SetDataContexts()
        {
            // Set General Settings data context
            var generalSettingsBorder = this.FindName("GeneralSettingsBorder") as Border;
            if (generalSettingsBorder != null)
            {
                generalSettingsBorder.DataContext = _settingsViewModel;
            }
        }

        /// <summary>
        /// Initializes dial configuration panels dynamically.
        /// </summary>
        private void InitializeDialPanels()
        {
            var dialsPanel = this.FindName("DialsPanel") as StackPanel;
            if (dialsPanel == null) return;

            int dialNumber = 1;
            foreach (var dialConfig in _configuration.Dials)
            {
                var viewModel = new DialConfigurationViewModel(dialConfig, dialNumber);
                _dialViewModels.Add(viewModel);

                // Load sensor data if HWInfo is available
                if (_hwInfoController != null && _hwInfoController.IsConnected)
                {
                    var readings = _hwInfoController.GetAllSensorReadings();
                    viewModel.LoadSensorData(readings);
                }

                var panel = new DialConfigurationPanel
                {
                    DataContext = viewModel
                };
                
                // Set VU1 controller for image uploads
                panel.SetVU1Controller(_vu1Controller);

                dialsPanel.Children.Add(panel);
                dialNumber++;
            }
        }

        /// <summary>
        /// Applies changes without closing the window.
        /// </summary>
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyChanges();
                
                MessageBox.Show(
                    "Settings applied successfully.\n\nNote: Some changes may require restarting the application to take full effect.",
                    "Settings Applied",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to apply settings:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Closes the settings window and saves changes.
        /// </summary>
        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyChanges();
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to save settings:\n\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Cancels any changes and closes the dialog.
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Closes the settings window without saving changes.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Handles title bar drag to move window.
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }

        /// <summary>
        /// Applies all changes from view models to the configuration and saves to disk.
        /// </summary>
        private void ApplyChanges()
        {
            // Apply app settings changes
            _settingsViewModel.ApplyChanges();

            // Apply dial configuration changes
            foreach (var viewModel in _dialViewModels)
            {
                viewModel.ApplyChanges();
            }

            // Validate configuration
            if (!_configuration.Validate(out var errors))
            {
                string errorMessage = string.Join(Environment.NewLine, errors);
                throw new InvalidOperationException($"Configuration validation failed:\n\n{errorMessage}");
            }

            // Save to disk
            string configPath = ConfigManager.GetDefaultConfigPath();
            var manager = new ConfigManager(configPath);
            manager.Save(_configuration);

            System.Diagnostics.Debug.WriteLine($"[SettingsWindow] Configuration saved to: {configPath}");
        }
    }
}
