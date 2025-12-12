// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

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
        private bool _isFirstRun = false;

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
        /// Constructor that accepts an existing configuration (used for first-run mode).
        /// </summary>
        public SettingsWindow(DialsConfiguration existingConfiguration)
        {
            InitializeComponent();
            
            // Use the provided configuration (already has discovered dial UIDs)
            _configuration = existingConfiguration;
            
            // Initialize view models
            _settingsViewModel = new SettingsViewModel(_configuration.AppSettings);
            _dialViewModels = new List<DialConfigurationViewModel>();

            // Initialize after InitializeComponent
            Loaded += SettingsWindow_Loaded;
        }

        /// <summary>
        /// Sets the window to first-run mode where Cancel button is hidden
        /// and closing the window exits the application.
        /// </summary>
        public void SetFirstRunMode(bool isFirstRun)
        {
            _isFirstRun = isFirstRun;
            
            if (_isFirstRun)
            {
                System.Diagnostics.Debug.WriteLine("[SettingsWindow] First-run mode enabled");
                Title = "VUWare - Initial Setup";
                
                // Hide Cancel button in first-run mode
                if (CancelButton != null)
                {
                    CancelButton.Visibility = Visibility.Collapsed;
                }
                
                // Hide Apply button in first-run mode (confusing for initial setup)
                if (ApplyButton != null)
                {
                    ApplyButton.Visibility = Visibility.Collapsed;
                }
            }
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
                
                if (readings != null)
                {
                    foreach (var dialViewModel in _dialViewModels)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SettingsWindow] Loading data for Dial #{dialViewModel.DialNumber}");
                        dialViewModel.LoadSensorData(readings);
                    }
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
            
            // Show welcome message in first-run mode
            if (_isFirstRun)
            {
                var firstRunWelcome = this.FindName("FirstRunWelcome") as Border;
                if (firstRunWelcome != null)
                {
                    firstRunWelcome.Visibility = Visibility.Visible;
                    System.Diagnostics.Debug.WriteLine("[SettingsWindow] Showing first-run welcome message");
                }
            }
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

            // Get physical dial count if VU1 controller is available
            int? physicalDialCount = _vu1Controller?.DialCount;

            // Get active dials based on effective dial count (respects dialCountOverride and physical dials)
            var activeDials = _configuration.GetActiveDials(physicalDialCount);
            
            string physicalInfo = physicalDialCount.HasValue ? $", Physical: {physicalDialCount.Value}" : "";
            System.Diagnostics.Debug.WriteLine($"[SettingsWindow] Initializing {activeDials.Count} active dial panels (Configured: {_configuration.Dials.Count}{physicalInfo})");

            int dialNumber = 1;
            foreach (var dialConfig in activeDials)
            {
                var viewModel = new DialConfigurationViewModel(dialConfig, dialNumber);
                _dialViewModels.Add(viewModel);

                // Load sensor data if HWInfo is available
                if (_hwInfoController != null && _hwInfoController.IsConnected)
                {
                    var readings = _hwInfoController.GetAllSensorReadings();
                    if (readings != null)
                    {
                        viewModel.LoadSensorData(readings);
                    }
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
            
            System.Diagnostics.Debug.WriteLine($"[SettingsWindow] Created {_dialViewModels.Count} dial configuration panels");
        }

        /// <summary>
        /// Applies changes without closing the window.
        /// </summary>
        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyChanges();

                // Trigger reload in MainWindow if available
                if (Owner is MainWindow mainWindow)
                {
                    await mainWindow.ReloadConfiguration();
                }
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
        private async void OKButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyChanges();

                // In first-run mode, just close with DialogResult = true
                // MainWindow will handle starting monitoring after the dialog closes
                if (_isFirstRun)
                {
                    System.Diagnostics.Debug.WriteLine("[SettingsWindow] First-run setup complete - closing dialog");
                    DialogResult = true;
                    Close();
                }
                else
                {
                    // Normal mode - trigger reload in MainWindow if available
                    if (Owner is MainWindow mainWindow)
                    {
                        await mainWindow.ReloadConfiguration();
                    }

                    DialogResult = true;
                    Close();
                }
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
            // In first-run mode, ask for confirmation before exiting the application
            if (_isFirstRun)
            {
                var result = MessageBox.Show(
                    "Are you sure you want to exit?\n\nVUWare requires initial configuration to function properly.",
                    "Exit VUWare?",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    // Exit the entire application
                    Application.Current.Shutdown();
                }
                return;
            }
            
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

            // Validate configuration BEFORE changing RunInit
            // In first-run mode, skip sensor validation since user just configured them
            if (!_configuration.Validate(out var errors, skipSensorValidation: _isFirstRun))
            {
                string errorMessage = string.Join(Environment.NewLine, errors);
                throw new InvalidOperationException($"Configuration validation failed:\n\n{errorMessage}");
            }

            // If this is first run, set RunInit to false so we don't show setup again
            if (_isFirstRun)
            {
                _configuration.AppSettings.RunInit = false;
                System.Diagnostics.Debug.WriteLine("[SettingsWindow] Setting RunInit to false - initial setup complete");
            }

            // Save to disk
            string configPath = ConfigManager.GetDefaultConfigPath();
            var manager = new ConfigManager(configPath);
            manager.Save(_configuration);

            System.Diagnostics.Debug.WriteLine($"[SettingsWindow] Configuration saved to: {configPath}");
        }
    }
}
