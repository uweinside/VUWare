using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VUWare.App.Models;
using VUWare.App.Services;

namespace VUWare.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DialsConfiguration? _config;
        private AppInitializationService? _initService;
        private SensorMonitoringService? _monitoringService;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfiguration();
            StartInitialization();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _monitoringService?.Dispose();
            _initService?.Dispose();
        }

        /// <summary>
        /// Loads the configuration file from disk.
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                string configPath = ConfigManager.GetDefaultConfigPath();
                _config = ConfigManager.LoadDefault();

                if (_config == null)
                {
                    MessageBox.Show(
                        $"No dial configuration found.\n\n" +
                        $"Checked location: {configPath}\n\n" +
                        $"Please ensure dials-config.json exists in either:\n" +
                        $"1. AppData\\VUWare\\ directory, or\n" +
                        $"2. The application's Config\\ directory",
                        "Configuration Not Found",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    StatusButton.Content = "Configuration Error";
                    StatusButton.IsEnabled = false;
                    return;
                }

                // Validate configuration
                if (!_config.Validate(out var errors))
                {
                    string errorMessage = string.Join(Environment.NewLine, errors);
                    MessageBox.Show(
                        $"Configuration validation failed:\n\n{errorMessage}",
                        "Configuration Invalid",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    StatusButton.Content = "Configuration Error";
                    StatusButton.IsEnabled = false;
                    return;
                }

                if (_config.AppSettings.DebugMode)
                {
                    MessageBox.Show(
                        $"✓ Configuration loaded successfully\n\n" +
                        $"Dials: {_config.Dials.Count}\n" +
                        $"Config file: {configPath}",
                        "Debug: Configuration Loaded",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load configuration:\n\n{ex.GetType().Name}: {ex.Message}\n\n" +
                    $"Base directory: {AppDomain.CurrentDomain.BaseDirectory}",
                    "Configuration Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                StatusButton.Content = "Configuration Error";
                StatusButton.IsEnabled = false;
            }
        }

        /// <summary>
        /// Starts the application initialization process on a background thread.
        /// </summary>
        private void StartInitialization()
        {
            if (_config == null)
            {
                return;
            }

            // Create and configure initialization service
            _initService = new AppInitializationService(_config);

            // Subscribe to status change events
            _initService.OnStatusChanged += InitService_OnStatusChanged;
            _initService.OnError += InitService_OnError;
            _initService.OnInitializationComplete += InitService_OnInitializationComplete;

            // Start initialization on background thread
            _initService.StartInitialization();
        }

        /// <summary>
        /// Handles status updates from the initialization service.
        /// </summary>
        private void InitService_OnStatusChanged(AppInitializationService.InitializationStatus status)
        {
            // Update UI on main thread
            Dispatcher.Invoke(() =>
            {
                StatusButton.Content = status switch
                {
                    AppInitializationService.InitializationStatus.ConnectingDials => "Connecting Dials",
                    AppInitializationService.InitializationStatus.InitializingDials => "Initializing Dials",
                    AppInitializationService.InitializationStatus.ConnectingHWInfo => "Connecting HWInfo Sensors",
                    AppInitializationService.InitializationStatus.Monitoring => "Monitoring",
                    AppInitializationService.InitializationStatus.Failed => "Initialization Failed",
                    _ => "Unknown Status"
                };

                // Change button color based on status
                UpdateStatusButtonColor(status);
            });
        }

        /// <summary>
        /// Handles error messages from the initialization service.
        /// </summary>
        private void InitService_OnError(string errorMessage)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    errorMessage,
                    "Initialization Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            });
        }

        /// <summary>
        /// Handles successful completion of initialization.
        /// </summary>
        private void InitService_OnInitializationComplete()
        {
            Dispatcher.Invoke(() =>
            {
                // Start monitoring
                StartMonitoring();

                if (_config?.AppSettings.DebugMode ?? false)
                {
                    MessageBox.Show(
                        $"✓ Initialization Complete\n\n" +
                        $"Dials: {_initService?.GetVU1Controller().DialCount}\n" +
                        $"HWInfo Connected: {_initService?.GetHWInfo64Controller().IsConnected}",
                        "Debug: Ready",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            });
        }

        /// <summary>
        /// Starts the sensor monitoring service.
        /// </summary>
        private void StartMonitoring()
        {
            if (_initService == null || _config == null)
                return;

            try
            {
                var vu1 = _initService.GetVU1Controller();
                var hwInfo = _initService.GetHWInfo64Controller();

                // Log diagnostics if debug mode is enabled
                if (_config.AppSettings.DebugMode)
                {
                    var diagnostics = new DiagnosticsService(hwInfo);
                    var report = diagnostics.GetDiagnosticsReport();
                    System.Diagnostics.Debug.WriteLine(report);
                    System.Diagnostics.Debug.WriteLine("=== Starting Monitoring Service ===");
                }

                // Create and start monitoring service
                _monitoringService = new SensorMonitoringService(vu1, hwInfo, _config);
                _monitoringService.OnDialUpdated += MonitoringService_OnDialUpdated;
                _monitoringService.OnError += MonitoringService_OnError;
                _monitoringService.Start();

                if (_config.AppSettings.DebugMode)
                {
                    System.Diagnostics.Debug.WriteLine($"✓ Monitoring service started");
                    System.Diagnostics.Debug.WriteLine($"  IsMonitoring: {_monitoringService.IsMonitoring}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to start monitoring: {ex.Message}\n\n" +
                    $"{ex.StackTrace}",
                    "Monitoring Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                System.Diagnostics.Debug.WriteLine($"Error starting monitoring: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Handles dial updates from the monitoring service.
        /// </summary>
        private void MonitoringService_OnDialUpdated(string dialUid, DialSensorUpdate update)
        {
            Dispatcher.Invoke(() =>
            {
                // Map dial UID to TextBlocks
                TextBlock? percentageBlock = dialUid switch
                {
                    "290063000750524834313020" => Dial1Percentage,
                    "870056000650564139323920" => Dial2Percentage,
                    "7B006B000650564139323920" => Dial3Percentage,
                    "31003F000650564139323920" => Dial4Percentage,
                    _ => null
                };

                TextBlock? displayNameBlock = dialUid switch
                {
                    "290063000750524834313020" => Dial1DisplayName,
                    "870056000650564139323920" => Dial2DisplayName,
                    "7B006B000650564139323920" => Dial3DisplayName,
                    "31003F000650564139323920" => Dial4DisplayName,
                    _ => null
                };

                Border? dialPanel = dialUid switch
                {
                    "290063000750524834313020" => Dial1Button,
                    "870056000650564139323920" => Dial2Button,
                    "7B006B000650564139323920" => Dial3Button,
                    "31003F000650564139323920" => Dial4Button,
                    _ => null
                };

                if (percentageBlock == null || displayNameBlock == null || dialPanel == null)
                    return;

                // Get dial configuration for this UID
                var dialConfig = _config?.Dials.FirstOrDefault(d => d.DialUid == dialUid);
                string displayValue;

                if (dialConfig?.DisplayFormat == "value")
                {
                    // Display actual sensor value with unit
                    displayValue = $"{update.SensorValue:F1}{dialConfig.DisplayUnit}";
                }
                else
                {
                    // Display percentage (default)
                    displayValue = $"{update.DialPercentage}%";
                }

                // Update text blocks
                percentageBlock.Text = displayValue;
                displayNameBlock.Text = update.DisplayName;

                // Update panel background color based on status
                if (update.IsCritical)
                {
                    // Red for critical
                    dialPanel.Background = new SolidColorBrush(Colors.Red);
                    percentageBlock.Foreground = new SolidColorBrush(Colors.White);
                    displayNameBlock.Foreground = new SolidColorBrush(Colors.White);
                }
                else if (update.IsWarning)
                {
                    // Orange for warning
                    dialPanel.Background = new SolidColorBrush(Color.FromRgb(255, 165, 0));
                    percentageBlock.Foreground = new SolidColorBrush(Colors.Black);
                    displayNameBlock.Foreground = new SolidColorBrush(Colors.Black);
                }
                else
                {
                    // Light gray for normal
                    dialPanel.Background = new SolidColorBrush(Color.FromRgb(204, 204, 204));
                    percentageBlock.Foreground = new SolidColorBrush(Colors.Black);
                    displayNameBlock.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
                }
            });
        }

        /// <summary>
        /// Handles errors from the monitoring service.
        /// </summary>
        private void MonitoringService_OnError(string errorMessage)
        {
            Dispatcher.Invoke(() =>
            {
                StatusButton.Content = $"Error: {errorMessage}";
                StatusButton.Background = new SolidColorBrush(Colors.Red);
                StatusButton.Foreground = new SolidColorBrush(Colors.White);
            });
        }

        /// <summary>
        /// Updates the status button color based on current initialization state.
        /// </summary>
        private void UpdateStatusButtonColor(AppInitializationService.InitializationStatus status)
        {
            switch (status)
            {
                case AppInitializationService.InitializationStatus.ConnectingDials:
                case AppInitializationService.InitializationStatus.InitializingDials:
                case AppInitializationService.InitializationStatus.ConnectingHWInfo:
                    // Yellow for in-progress
                    StatusButton.Background = new SolidColorBrush(Color.FromRgb(255, 200, 0));
                    StatusButton.Foreground = new SolidColorBrush(Colors.Black);
                    break;

                case AppInitializationService.InitializationStatus.Monitoring:
                    // Green for ready
                    StatusButton.Background = new SolidColorBrush(Color.FromRgb(0, 200, 0));
                    StatusButton.Foreground = new SolidColorBrush(Colors.White);
                    break;

                case AppInitializationService.InitializationStatus.Failed:
                    // Red for error
                    StatusButton.Background = new SolidColorBrush(Colors.Red);
                    StatusButton.Foreground = new SolidColorBrush(Colors.White);
                    break;

                default:
                    // Gray for idle
                    StatusButton.Background = new SolidColorBrush(Color.FromRgb(200, 200, 200));
                    StatusButton.Foreground = new SolidColorBrush(Colors.Black);
                    break;
            }
        }
    }
}