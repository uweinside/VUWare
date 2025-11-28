using System;
using System.Collections.Generic;
using System.Windows;
using VUWare.App.Models;
using VUWare.App.Services;
using VUWare.App.ViewModels;

namespace VUWare.App
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private DialsConfiguration? _config;
        private List<DialConfigurationViewModel> _dialViewModels;

        public SettingsWindow()
        {
            InitializeComponent();
            _dialViewModels = new List<DialConfigurationViewModel>();
            Loaded += SettingsWindow_Loaded;
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfiguration();
        }

        /// <summary>
        /// Loads the current configuration and initializes the settings UI.
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                _config = ConfigManager.LoadDefault();
                if (_config == null)
                {
                    MessageBox.Show(
                        "Failed to load configuration for settings.",
                        "Configuration Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // Load general settings
                DebugModeCheckBox.IsChecked = _config.AppSettings.DebugMode;
                MonitoringIntervalTextBox.Text = _config.AppSettings.GlobalUpdateIntervalMs.ToString();

                // Load dial configurations into panels
                LoadDialConfigurations();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading configuration: {ex.Message}",
                    "Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Creates and loads all dial configuration panels.
        /// </summary>
        private void LoadDialConfigurations()
        {
            if (_config == null)
                return;

            _dialViewModels.Clear();
            DialConfigPanelsContainer.Children.Clear();

            foreach (var dialConfig in _config.Dials)
            {
                // Create view model
                var viewModel = new DialConfigurationViewModel(dialConfig);
                _dialViewModels.Add(viewModel);

                // Create and add panel
                var panel = new DialConfigurationPanel();
                panel.SetViewModel(viewModel);
                DialConfigPanelsContainer.Children.Add(panel);
            }
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
        /// Applies the current settings without closing the dialog.
        /// </summary>
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!SaveSettings())
                {
                    MessageBox.Show(
                        "Please fix the validation errors before applying.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                MessageBox.Show(
                    "Settings applied successfully.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error applying settings: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Applies settings and closes the dialog.
        /// </summary>
        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!SaveSettings())
                {
                    MessageBox.Show(
                        "Please fix the validation errors before saving.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error saving settings: {ex.Message}",
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
        /// Saves all settings to configuration and persists to file.
        /// </summary>
        private bool SaveSettings()
        {
            if (_config == null)
                return false;

            // Save general settings
            _config.AppSettings.DebugMode = DebugModeCheckBox.IsChecked ?? false;
            
            if (int.TryParse(MonitoringIntervalTextBox.Text, out int interval) && interval >= 100)
            {
                _config.AppSettings.GlobalUpdateIntervalMs = interval;
            }
            else
            {
                MessageBox.Show(
                    "Monitoring interval must be at least 100 ms.",
                    "Invalid Input",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            // Apply dial configuration changes
            foreach (var viewModel in _dialViewModels)
            {
                viewModel.ApplyChanges();
            }

            // Validate configuration
            if (!_config.Validate(out var errors))
            {
                string errorMessage = string.Join(Environment.NewLine, errors);
                MessageBox.Show(
                    $"Configuration validation failed:\n\n{errorMessage}",
                    "Validation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }

            // Save to file
            string configPath = ConfigManager.GetDefaultConfigPath();
            var configManager = new ConfigManager(configPath);
            configManager.Save(_config);

            System.Diagnostics.Debug.WriteLine($"Configuration saved to: {configPath}");
            return true;
        }
    }
}
