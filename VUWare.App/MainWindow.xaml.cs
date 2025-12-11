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
using System.Linq;
using WpfColors = System.Windows.Media.Colors;
using VULib = VUWare.Lib;

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
        private SystemTrayManager? _trayManager;

        public MainWindow()
        {
            InitializeComponent();
            
            // Disable settings button until initialization is complete
            SettingsButton.IsEnabled = false;
            
            // Set window icon from PNG file
            try
            {
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VU1_Icon.png");
                if (System.IO.File.Exists(iconPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    Icon = bitmap;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load window icon: {ex.Message}");
            }
            
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            StateChanged += MainWindow_StateChanged;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize tray manager with this window first
            InitializeSystemTray();
            
            // Then load configuration and start monitoring
            LoadConfiguration();
            
            // Apply startMinimized setting from configuration
            if (_config?.AppSettings.StartMinimized == true)
            {
                WindowState = WindowState.Minimized;
                System.Diagnostics.Debug.WriteLine("[MainWindow] Starting minimized based on configuration");
            }
            
            StartInitialization();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Perform the actual shutdown
            PerformGracefulShutdown();
        }

        /// <summary>
        /// Performs graceful shutdown of all services and resets hardware to safe state.
        /// Can be called from window closing OR from App.SessionEnding.
        /// </summary>
        public void PerformGracefulShutdown()
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Starting graceful shutdown sequence");
            
            // Step 1: Stop monitoring service first
            if (_monitoringService != null)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Stopping sensor monitoring service");
                _monitoringService.Stop();
                _monitoringService.Dispose();
                _monitoringService = null;
            }
            
            // Step 2: Stop HWInfo64 polling
            // BUT DON'T call Disconnect() yet - that would cancel the shutdown commands!
            if (_initService != null)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Stopping HWInfo64 polling");
                _initService.GetHWInfo64Controller().Disconnect();
            }
            
            // Give loops to stop
            System.Threading.Thread.Sleep(200);
            
            // Step 3: Send shutdown commands directly using simple synchronous serial commands
            // This bypasses all the async/cancellation infrastructure
            ResetAllDialValues();
            TurnOffAllDialLights();
            
            // Step 4: Dispose services
            System.Diagnostics.Debug.WriteLine("[MainWindow] Disposing initialization service");
            _initService?.Dispose();
            _initService = null;
            
            System.Diagnostics.Debug.WriteLine("[MainWindow] Graceful shutdown sequence complete");
        }

        /// <summary>
        /// Resets all dial needle positions to zero on the physical devices.
        /// Uses direct serial port commands to avoid cancellation issues.
        /// </summary>
        private void ResetAllDialValues()
        {
            if (_initService == null || _config == null)
                return;

            try
            {
                var vu1 = _initService.GetVU1Controller();

                if (!vu1.IsConnected || !vu1.IsInitialized)
                {
                    System.Diagnostics.Debug.WriteLine("VU1 not connected or initialized, skipping dial value reset");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("Resetting all dial values to zero...");

                var dials = vu1.GetAllDials();
                System.Diagnostics.Debug.WriteLine($"Resetting {dials.Count} dials to 0%");

                // Get configured delay between commands
                int commandDelay = _config.AppSettings.SerialCommandDelayMs;
                System.Diagnostics.Debug.WriteLine($"Using command delay: {commandDelay}ms");

                int successCount = 0;
                foreach (var dial in dials.Values)
                {
                    try
                    {
                        // Build command directly: >CCDDLLLLDATA
                        // CC=03 (SET), DD=04 (DIAL_POSITION), LLLL=0002 (2 bytes), DATA=XXPP (dial index + percentage)
                        string command = $">03040002{dial.Index:X2}00";
                        System.Diagnostics.Debug.WriteLine($"Sending reset command for {dial.Name}: {command}");
                        
                        // Send using synchronous method that bypasses cancellation
                        string response = vu1.GetType()
                            .GetField("_serialPort", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                            .GetValue(vu1) is VULib.SerialPortManager serialPort
                            ? serialPort.SendCommandSync(command, 2000)
                            : "";
                        
                        if (response.StartsWith("<") && response.Length >= 9)
                        {
                            successCount++;
                            System.Diagnostics.Debug.WriteLine($"? {dial.Name} reset to 0%");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"? {dial.Name} reset failed - no response");
                        }
                        
                        // Add delay between commands to prevent overwhelming the hub
                        System.Threading.Thread.Sleep(commandDelay);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error resetting {dial.Name}: {ex.Message}");
                        // Still delay even on error to prevent rapid retry
                        System.Threading.Thread.Sleep(commandDelay);
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"Dial reset complete: {successCount}/{dials.Count} successful");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Critical error during dial reset: {ex.Message}");
            }
        }

        /// <summary>
        /// Turns off all dial backlights when the application closes.
        /// Uses direct serial port commands to avoid cancellation issues.
        /// </summary>
        private void TurnOffAllDialLights()
        {
            if (_initService == null || _config == null)
                return;

            try
            {
                var vu1 = _initService.GetVU1Controller();
                
                if (!vu1.IsConnected || !vu1.IsInitialized)
                {
                    System.Diagnostics.Debug.WriteLine("VU1 not connected or initialized, skipping light shutdown");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("Turning off all dial backlights...");

                var dials = vu1.GetAllDials();
                System.Diagnostics.Debug.WriteLine($"Turning off {dials.Count} dial backlights");

                // Get configured delay between commands
                int commandDelay = _config.AppSettings.SerialCommandDelayMs;
                System.Diagnostics.Debug.WriteLine($"Using command delay: {commandDelay}ms");

                int successCount = 0;
                foreach (var dial in dials.Values)
                {
                    try
                    {
                        // Build command directly: >CCDDLLLLDATA
                        // CC=13 (SET_RGB), DD=03 (dial index embedded), LLLL=0005 (5 bytes), DATA=XXRRGGBBWW
                        string command = $">13030005{dial.Index:X2}00000000";
                        System.Diagnostics.Debug.WriteLine($"Sending backlight off for {dial.Name}: {command}");
                        
                        // Send using synchronous method that bypasses cancellation
                        string response = vu1.GetType()
                            .GetField("_serialPort", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?
                            .GetValue(vu1) is VULib.SerialPortManager serialPort
                            ? serialPort.SendCommandSync(command, 2000)
                            : "";
                        
                        if (response.StartsWith("<") && response.Length >= 9)
                        {
                            successCount++;
                            System.Diagnostics.Debug.WriteLine($"? {dial.Name} backlight off");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"? {dial.Name} backlight failed - no response");
                        }
                        
                        // Add delay between commands to prevent overwhelming the hub
                        System.Threading.Thread.Sleep(commandDelay);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error turning off {dial.Name}: {ex.Message}");
                        // Still delay even on error to prevent rapid retry
                        System.Threading.Thread.Sleep(commandDelay);
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"Backlight shutdown complete: {successCount}/{dials.Count} successful");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Critical error during backlight shutdown: {ex.Message}");
            }
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
                    StatusText.Text = "Configuration Error";
                    StatusText.Foreground = new SolidColorBrush(WpfColors.Red);
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
                    StatusText.Text = "Configuration Error";
                    StatusText.Foreground = new SolidColorBrush(WpfColors.Red);
                    return;
                }

                // Initialize dial panel colors based on configuration
                InitializeDialPanelColors();

                if (_config.AppSettings.DebugMode)
                {
                    MessageBox.Show(
                        $"? Configuration loaded successfully\n\n" +
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
                StatusText.Text = "Configuration Error";
                StatusText.Foreground = new SolidColorBrush(WpfColors.Red);
            }
        }

        /// <summary>
        /// Initializes dial panel colors based on their configuration color modes.
        /// </summary>
        private void InitializeDialPanelColors()
        {
            if (_config == null)
                return;

            // Map dial UIDs to UI elements
            var dialPanels = new Dictionary<string, (Border panel, TextBlock percentage, TextBlock displayName)>
            {
                { "290063000750524834313020", (Dial1Button, Dial1Percentage, Dial1DisplayName) },
                { "870056000650564139323920", (Dial2Button, Dial2Percentage, Dial2DisplayName) },
                { "7B006B000650564139323920", (Dial3Button, Dial3Percentage, Dial3DisplayName) },
                { "31003F000650564139323920", (Dial4Button, Dial4Percentage, Dial4DisplayName) }
            };

            foreach (var dial in _config.Dials)
            {
                if (!dialPanels.TryGetValue(dial.DialUid, out var elements))
                    continue;

                var (panel, percentageBlock, displayNameBlock) = elements;
                
                // Use neutral dark gray for all dials until monitoring starts
                Color panelColor = Color.FromRgb(80, 80, 80);
                Color textColor = WpfColors.White;
                Color subtextColor = Color.FromRgb(150, 150, 150);

                // Apply neutral colors
                panel.Background = new SolidColorBrush(panelColor);
                percentageBlock.Foreground = new SolidColorBrush(textColor);
                displayNameBlock.Foreground = new SolidColorBrush(subtextColor);
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
            _initService.OnHWInfoRetryStatusChanged += InitService_OnHWInfoRetryStatusChanged;
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
                StatusText.Text = status switch
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
        /// Handles HWInfo connection retry status updates.
        /// </summary>
        private void InitService_OnHWInfoRetryStatusChanged(int retryCount, bool isConnected, int elapsedSeconds)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"Connecting to HWiNFO64© [{retryCount}]";
                
                if (isConnected)
                {
                    System.Diagnostics.Debug.WriteLine($"[UI] HWInfo connected after {retryCount} attempts in {elapsedSeconds}s");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[UI] HWInfo retry #{retryCount} after {elapsedSeconds}s elapsed");
                }
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
                
                // Enable settings button now that monitoring is running
                SettingsButton.IsEnabled = true;
                SettingsButton.ToolTip = "Settings";
                System.Diagnostics.Debug.WriteLine("[MainWindow] Settings button enabled - initialization complete");

                if (_config?.AppSettings.DebugMode == true)
                {
                    var dialCount = _initService?.GetVU1Controller().DialCount ?? 0;
                    var hwInfoConnected = _initService?.GetHWInfo64Controller().IsConnected ?? false;
                    
                    MessageBox.Show(
                        $"? Initialization Complete\n\n" +
                        $"Dials: {dialCount}\n" +
                        $"HWInfo Connected: {hwInfoConnected}",
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
                    System.Diagnostics.Debug.WriteLine($"? Monitoring service started");
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
                if (_config == null)
                    return;

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
                var dialConfig = _config.Dials.FirstOrDefault(d => d.DialUid == dialUid);
                string displayValue;

                if (dialConfig?.DisplayFormat == "value")
                {
                    // Display actual sensor value with unit
                    displayValue = $"{update.SensorValue:F1}{dialConfig!.DisplayUnit}";
                }
                else
                {
                    // Display percentage (default)
                    displayValue = $"{update.DialPercentage}%";
                }

                // Update text blocks
                percentageBlock.Text = displayValue;
                displayNameBlock.Text = update.DisplayName;

                // Determine color based on dial configuration's color mode
                Color panelColor = Color.FromRgb(204, 204, 204); // Default light gray
                Color textColor = WpfColors.Black;
                Color subtextColor = Color.FromRgb(102, 102, 102);

                if (dialConfig?.ColorConfig.ColorMode == "off")
                {
                    // Off mode: keep default colors
                    panelColor = Color.FromRgb(204, 204, 204);
                    textColor = WpfColors.Black;
                    subtextColor = Color.FromRgb(102, 102, 102);
                }
                else if (dialConfig?.ColorConfig.ColorMode == "static")
                {
                    // Static mode: use staticColor from config
                    panelColor = GetColorFromString(dialConfig!.ColorConfig.StaticColor);
                    textColor = GetContrastingTextColor(panelColor);
                    subtextColor = GetContrastingSubtextColor(panelColor);
                }
                else // threshold mode (default)
                {
                    // Threshold mode: change color based on sensor status
                    if (update.IsCritical)
                    {
                        // Critical state: use the configured critical color
                        panelColor = GetColorFromString(dialConfig!.ColorConfig.CriticalColor);
                        textColor = GetContrastingTextColor(panelColor);
                        subtextColor = GetContrastingSubtextColor(panelColor);
                    }
                    else if (update.IsWarning)
                    {
                        // Warning state: use the configured warning color
                        panelColor = GetColorFromString(dialConfig!.ColorConfig.WarningColor);
                        textColor = GetContrastingTextColor(panelColor);
                        subtextColor = GetContrastingSubtextColor(panelColor);
                    }
                    else
                    {
                        // Normal state: use the configured normal color
                        panelColor = GetColorFromString(dialConfig!.ColorConfig.NormalColor);
                        textColor = GetContrastingTextColor(panelColor);
                        subtextColor = GetContrastingSubtextColor(panelColor);
                    }
                }

                // Apply colors
                dialPanel.Background = new SolidColorBrush(panelColor);
                percentageBlock.Foreground = new SolidColorBrush(textColor);
                displayNameBlock.Foreground = new SolidColorBrush(subtextColor);
            });
        }

        /// <summary>
        /// Determines if text should be white or black based on background color brightness.
        /// </summary>
        private Color GetContrastingTextColor(Color backgroundColor)
        {
            // Calculate luminance
            double luminance = (0.299 * backgroundColor.R + 0.587 * backgroundColor.G + 0.114 * backgroundColor.B) / 255;
            return luminance > 0.5 ? WpfColors.Black : WpfColors.White;
        }

        /// <summary>
        /// Determines the contrasting color for subtext (slightly more opacity).
        /// </summary>
        private Color GetContrastingSubtextColor(Color backgroundColor)
        {
            double luminance = (0.299 * backgroundColor.R + 0.587 * backgroundColor.G + 0.114 * backgroundColor.B) / 255;
            return luminance > 0.5 ? Color.FromRgb(102, 102, 102) : Color.FromRgb(200, 200, 200);
        }

        /// <summary>
        /// Handles errors from the monitoring service.
        /// </summary>
        private void MonitoringService_OnError(string errorMessage)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"Error: {errorMessage}";
                StatusText.Foreground = new SolidColorBrush(WpfColors.Red);
            });
        }

        /// <summary>
        /// Updates the status text and color based on current initialization state.
        /// </summary>
        private void UpdateStatusButtonColor(AppInitializationService.InitializationStatus status)
        {
            Color textColor = status switch
            {
                AppInitializationService.InitializationStatus.ConnectingDials => Color.FromRgb(255, 200, 0),    // Yellow
                AppInitializationService.InitializationStatus.InitializingDials => Color.FromRgb(255, 200, 0),  // Yellow
                AppInitializationService.InitializationStatus.ConnectingHWInfo => Color.FromRgb(255, 200, 0),   // Yellow
                AppInitializationService.InitializationStatus.Monitoring => Color.FromRgb(0, 200, 0),           // Green
                AppInitializationService.InitializationStatus.Failed => WpfColors.Red,                             // Red
                _ => Color.FromRgb(200, 200, 200)                                                               // Gray
            };

            StatusText.Foreground = new SolidColorBrush(textColor);
        }

        /// <summary>
        /// Converts a color name string to a WPF Color.
        /// </summary>
        private Color GetColorFromString(string colorName)
        {
            return colorName switch
            {
                "Red" => Color.FromRgb(255, 0, 0),
                "Green" => Color.FromRgb(0, 255, 0),
                "Blue" => Color.FromRgb(0, 0, 255),
                "Yellow" => Color.FromRgb(255, 255, 0),
                "Cyan" => Color.FromRgb(0, 255, 255),
                "Magenta" => Color.FromRgb(255, 0, 255),
                "Orange" => Color.FromRgb(255, 165, 0),
                "Purple" => Color.FromRgb(128, 0, 128),
                "Pink" => Color.FromRgb(255, 192, 203),
                "White" => Color.FromRgb(255, 255, 255),
                "Off" => Color.FromRgb(0, 0, 0),
                _ => Color.FromRgb(204, 204, 204) // Default gray
            };
        }

        /// <summary>
        /// Handles title bar drag to move the window.
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // Double-click to maximize/restore (not applicable here since we don't allow maximize)
            }
            else
            {
                // Single click and drag to move window
                DragMove();
            }
        }

        /// <summary>
        /// Minimizes the window.
        /// </summary>
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// Closes the window.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Opens the settings dialog.
        /// </summary>
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            
            // Pass controllers to the settings window BEFORE showing it
            if (_initService != null)
            {
                if (_initService.GetHWInfo64Controller() != null)
                {
                    settingsWindow.SetHWInfo64Controller(_initService.GetHWInfo64Controller());
                }
                
                if (_initService.GetVU1Controller() != null)
                {
                    settingsWindow.SetVU1Controller(_initService.GetVU1Controller());
                }
            }
            
            // Now show the dialog
            bool? result = settingsWindow.ShowDialog();
            
            // If settings were saved, reload configuration
            if (result == true)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Settings saved, configuration may need reload on next restart");
                MessageBox.Show(
                    "Settings have been saved.\n\nSome changes may require restarting the application to take full effect.",
                    "Settings Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// Opens the about dialog.
        /// </summary>
        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            AboutDialog aboutDialog = new AboutDialog();
            aboutDialog.Owner = this;
            aboutDialog.ShowDialog();
        }

        /// <summary>
        /// Initializes the system tray manager with the main window.
        /// </summary>
        private void InitializeSystemTray()
        {
            // Get the tray manager from the application
            if (Application.Current is App app && app.TrayManager != null)
            {
                _trayManager = app.TrayManager;
                _trayManager.Initialize(this);
                System.Diagnostics.Debug.WriteLine("[MainWindow] System tray initialized");
            }
        }

        /// <summary>
        /// Handles window state changes to manage tray minimize/restore.
        /// </summary>
        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (_trayManager == null)
                return;

            if (WindowState == WindowState.Minimized)
            {
                // Disable UI updates when hidden - physical dials still update!
                _monitoringService?.SetUIUpdateEnabled(false);
                
                // Hide window to tray
                _trayManager.HideToTray();
                _trayManager.ShowIcon();
                System.Diagnostics.Debug.WriteLine("[MainWindow] Window minimized to tray - UI updates DISABLED");
            }
            else if (WindowState == WindowState.Normal)
            {
                // Re-enable UI updates when visible
                _monitoringService?.SetUIUpdateEnabled(true);
                
                // Restore window from tray
                _trayManager.HideIcon();
                ShowInTaskbar = true;
                System.Diagnostics.Debug.WriteLine("[MainWindow] Window restored from tray - UI updates ENABLED");
            }
        }
    }
}
