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
        private readonly Dictionary<string, Button> _dialButtons = new();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Map dial UIDs to buttons for quick access
            MapDialButtons();
            LoadConfiguration();
            StartInitialization();
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _monitoringService?.Dispose();
            _initService?.Dispose();
        }

        /// <summary>
        /// Maps dial configuration UIDs to their corresponding UI buttons.
        /// </summary>
        private void MapDialButtons()
        {
            _dialButtons.Clear();
            _dialButtons["DIAL_001"] = Dial1Button;
            _dialButtons["DIAL_002"] = Dial2Button;
            _dialButtons["DIAL_003"] = Dial3Button;
            _dialButtons["DIAL_004"] = Dial4Button;
        }

        /// <summary>
        /// Gets the button for a dial UID, or null if not found.
        /// </summary>
        private Button? GetDialButton(string dialUid)
        {
            // Try exact match first
            if (_dialButtons.TryGetValue(dialUid, out var button))
                return button;

            // If not found by configured ID, try matching by position in config
            if (_config != null)
            {
                for (int i = 0; i < _config.Dials.Count && i < 4; i++)
                {
                    if (_config.Dials[i].DialUid == dialUid)
                    {
                        return i switch
                        {
                            0 => Dial1Button,
                            1 => Dial2Button,
                            2 => Dial3Button,
                            3 => Dial4Button,
                            _ => null
                        };
                    }
                }
            }

            return null;
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
                var button = GetDialButton(dialUid);
                if (button == null)
                    return;

                // Update button appearance based on status
                if (update.IsCritical)
                {
                    // Red for critical
                    button.Background = new SolidColorBrush(Colors.Red);
                    button.Foreground = new SolidColorBrush(Colors.White);
                }
                else if (update.IsWarning)
                {
                    // Orange for warning
                    button.Background = new SolidColorBrush(Color.FromRgb(255, 165, 0));
                    button.Foreground = new SolidColorBrush(Colors.Black);
                }
                else
                {
                    // Green for normal
                    button.Background = new SolidColorBrush(Color.FromRgb(0, 200, 0));
                    button.Foreground = new SolidColorBrush(Colors.White);
                }

                // Set tooltip with sensor information
                button.ToolTip = update.GetTooltip();

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

                // Update button content with value and display name
                button.Content = new DialButtonContent
                {
                    Percentage = displayValue,
                    DisplayName = update.DisplayName
                };
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

    /// <summary>
    /// Data model for dial button content binding.
    /// </summary>
    public class DialButtonContent
    {
        public string Percentage { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}