using System;
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
                // Initialization complete - UI is now ready for monitoring
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