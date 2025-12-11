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
        private AvailableSensorsViewModel _sensorsViewModel;
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
            _sensorsViewModel = new AvailableSensorsViewModel();
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
                
                _sensorsViewModel.LoadSensors(_hwInfoController);
                UpdateSensorCount();
                
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
            
            // Initialize dial selection for image upload
            InitializeDialSelection();

            // Refresh sensors if HWInfo is available
            if (_hwInfoController != null && _hwInfoController.IsConnected)
            {
                _sensorsViewModel.LoadSensors(_hwInfoController);
                UpdateSensorCount();
            }
        }

        /// <summary>
        /// Sets data contexts for various tabs after controls are loaded.
        /// </summary>
        private void SetDataContexts()
        {
            // Find the TabControl
            var tabControl = this.Content as Grid;
            if (tabControl == null) return;

            var mainTabControl = FindVisualChild<TabControl>(tabControl);
            if (mainTabControl == null) return;

            // Set Application Settings tab data context
            if (mainTabControl.Items.Count > 1)
            {
                var appSettingsTab = mainTabControl.Items[1] as TabItem;
                if (appSettingsTab != null)
                {
                    var scrollViewer = appSettingsTab.Content as ScrollViewer;
                    if (scrollViewer?.Content is Border border)
                    {
                        border.DataContext = _settingsViewModel;
                    }
                }
            }

            // Set Available Sensors tab data context
            if (mainTabControl.Items.Count > 2)
            {
                var sensorsTab = mainTabControl.Items[2] as TabItem;
                if (sensorsTab != null)
                {
                    var grid = sensorsTab.Content as Grid;
                    if (grid != null)
                    {
                        grid.DataContext = _sensorsViewModel;
                    }
                }
            }
        }

        /// <summary>
        /// Helper method to find a child control by type.
        /// </summary>
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;
                
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
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

                dialsPanel.Children.Add(panel);
                dialNumber++;
            }
        }

        /// <summary>
        /// Initializes the dial selection combo box for image uploads.
        /// </summary>
        private void InitializeDialSelection()
        {
            var dialComboBox = this.FindName("DialSelectionComboBox") as ComboBox;
            if (dialComboBox == null) return;

            dialComboBox.ItemsSource = _dialViewModels;
        }

        /// <summary>
        /// Updates the sensor count display.
        /// </summary>
        private void UpdateSensorCount()
        {
            var sensorCountText = this.FindName("SensorCountText") as TextBlock;
            if (sensorCountText == null) return;

            int totalCount = _sensorsViewModel.AllSensors.Count;
            int filteredCount = _sensorsViewModel.FilteredSensors.Count;
            
            if (totalCount == 0)
            {
                sensorCountText.Text = "No sensors available. Ensure HWInfo64 is running with Shared Memory Support enabled.";
            }
            else if (string.IsNullOrWhiteSpace(_sensorsViewModel.SearchText))
            {
                sensorCountText.Text = $"Total sensors: {totalCount}";
            }
            else
            {
                sensorCountText.Text = $"Showing {filteredCount} of {totalCount} sensors";
            }
        }

        /// <summary>
        /// Handles sensor search text changes.
        /// </summary>
        private void SensorSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            _sensorsViewModel.SearchText = textBox.Text;
            UpdateSensorCount();
        }

        /// <summary>
        /// Refreshes the available sensors list.
        /// </summary>
        private void RefreshSensorsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_hwInfoController == null || !_hwInfoController.IsConnected)
            {
                MessageBox.Show(
                    "HWInfo64 is not connected. Please ensure HWInfo64 is running with Shared Memory Support enabled.",
                    "HWInfo64 Not Available",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Reload sensors for the available sensors view
            _sensorsViewModel.LoadSensors(_hwInfoController);
            UpdateSensorCount();
            
            // Reload sensor data for all dial view models
            var readings = _hwInfoController.GetAllSensorReadings();
            foreach (var dialViewModel in _dialViewModels)
            {
                dialViewModel.LoadSensorData(readings);
            }
            
            MessageBox.Show(
                $"Loaded {_sensorsViewModel.AllSensors.Count} sensors from HWInfo64.",
                "Sensors Refreshed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        /// <summary>
        /// Handles dial selection change for image upload.
        /// </summary>
        private void DialSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var uploadButton = this.FindName("UploadImageButton") as Button;
            if (comboBox == null || uploadButton == null) return;

            uploadButton.IsEnabled = comboBox.SelectedItem != null;
        }

        /// <summary>
        /// Handles image upload button click.
        /// </summary>
        private async void UploadImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialComboBox = this.FindName("DialSelectionComboBox") as ComboBox;
            var statusText = this.FindName("ImageUploadStatusText") as TextBlock;
            
            if (dialComboBox == null || statusText == null) return;

            if (_vu1Controller == null || !_vu1Controller.IsConnected)
            {
                MessageBox.Show(
                    "VU1 Hub is not connected. Please ensure the hub is connected before uploading images.",
                    "VU1 Not Connected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var selectedViewModel = dialComboBox.SelectedItem as DialConfigurationViewModel;
            if (selectedViewModel == null)
                return;

            // Find the dial configuration
            var dialConfig = _configuration.Dials.FirstOrDefault(d => d.DialUid == selectedViewModel.DialUid);
            if (dialConfig == null)
                return;

            // Open file dialog
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select Dial Face Image",
                Filter = "Image Files|*.png;*.bmp;*.jpg;*.jpeg|All Files|*.*",
                CheckFileExists = true
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            try
            {
                statusText.Text = "Loading and processing image...";
                statusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Yellow);

                // Load and process the image
                byte[] imageData = VUWare.Lib.ImageProcessor.LoadImageFile(openFileDialog.FileName);

                statusText.Text = $"Uploading to {selectedViewModel.DisplayName}...";

                // Upload to the dial
                bool success = await _vu1Controller.SetDisplayImageAsync(dialConfig.DialUid, imageData);

                if (success)
                {
                    statusText.Text = $"? Image successfully uploaded to {selectedViewModel.DisplayName}";
                    statusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                }
                else
                {
                    statusText.Text = $"? Failed to upload image to {selectedViewModel.DisplayName}";
                    statusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                }
            }
            catch (Exception ex)
            {
                statusText.Text = $"? Error: {ex.Message}";
                statusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                
                MessageBox.Show(
                    $"Failed to upload image:\n\n{ex.Message}",
                    "Upload Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
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
