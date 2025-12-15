// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
using VUWare.Lib.Sensors;
using VULib = VUWare.Lib;
using WpfColors = System.Windows.Media.Colors;

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
            
            // Step 2: Disconnect sensor provider
            if (_initService != null)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Disconnecting sensor provider");
                _initService.GetSensorProvider()?.Disconnect();
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

                // Apply dial visibility based on effective dial count
                ApplyDialVisibility();

                // Initialize dial panel colors based on configuration
                InitializeDialPanelColors();

                if (_config.AppSettings.DebugMode)
                {
                    int effectiveDialCount = _config.GetEffectiveDialCount();
                    string overrideInfo = _config.AppSettings.DialCountOverride.HasValue 
                        ? $" (Override: {_config.AppSettings.DialCountOverride.Value})" 
                        : "";
                    
                    MessageBox.Show(
                        $"? Configuration loaded successfully\n\n" +
                        $"Total Dials Configured: {_config.Dials.Count}\n" +
                        $"Active Dials: {effectiveDialCount}{overrideInfo}\n" +
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
        /// Sets the visibility of dial containers based on the effective dial count from configuration.
        /// </summary>
        /// <param name="physicalDialCount">Optional: Number of physically detected dials to limit visibility</param>
        private void ApplyDialVisibility(int? physicalDialCount = null)
        {
            if (_config == null)
                return;

            int effectiveDialCount = _config.GetEffectiveDialCount(physicalDialCount);
            
            string physicalInfo = physicalDialCount.HasValue ? $" (Physical: {physicalDialCount.Value})" : "";
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Applying dial visibility: {effectiveDialCount} active dials{physicalInfo}");

            // Set visibility for each dial container
            Dial1Container.Visibility = effectiveDialCount >= 1 ? Visibility.Visible : Visibility.Collapsed;
            Dial2Container.Visibility = effectiveDialCount >= 2 ? Visibility.Visible : Visibility.Collapsed;
            Dial3Container.Visibility = effectiveDialCount >= 3 ? Visibility.Visible : Visibility.Collapsed;
            Dial4Container.Visibility = effectiveDialCount >= 4 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Initializes dial panel colors based on their configuration color modes.
        /// During initial setup (RunInit=true), shows welcome panel instead of dials.
        /// </summary>
        private void InitializeDialPanelColors()
        {
            if (_config == null)
                return;

            bool isInitialSetup = _config.AppSettings.RunInit;
            int effectiveDialCount = _config.GetEffectiveDialCount();

            // Handle first-run experience: show welcome panel, hide dials
            if (isInitialSetup)
            {
                DialsPanel.Visibility = Visibility.Collapsed;
                WelcomePanel.Visibility = Visibility.Visible;
                return;
            }

            // Normal mode: show dials, hide welcome panel
            DialsPanel.Visibility = Visibility.Visible;
            WelcomePanel.Visibility = Visibility.Collapsed;

            // Define initialization colors for each dial position (red, green, yellow, cyan)
            var initColors = new[]
            {
                Color.FromRgb(255, 0, 0),    // Red for dial 1
                Color.FromRgb(0, 255, 0),    // Green for dial 2
                Color.FromRgb(255, 255, 0),  // Yellow for dial 3
                Color.FromRgb(0, 255, 255)   // Cyan for dial 4
            };

            // Map dial positions to UI elements
            var dialPanelsByPosition = new (Border panel, TextBlock percentage, TextBlock displayName)[]
            {
                (Dial1Button, Dial1Percentage, Dial1DisplayName),
                (Dial2Button, Dial2Percentage, Dial2DisplayName),
                (Dial3Button, Dial3Percentage, Dial3DisplayName),
                (Dial4Button, Dial4Percentage, Dial4DisplayName)
            };

            for (int i = 0; i < Math.Min(effectiveDialCount, 4); i++)
            {
                var (panel, percentageBlock, displayNameBlock) = dialPanelsByPosition[i];
                
                // Normal mode - use neutral dark gray until monitoring starts
                Color panelColor = Color.FromRgb(80, 80, 80);
                Color textColor = WpfColors.White;
                Color subtextColor = Color.FromRgb(150, 150, 150);

                panel.Background = new SolidColorBrush(panelColor);
                percentageBlock.Text = "--";
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
            _initService.OnSensorRetryStatusChanged += InitService_OnSensorRetryStatusChanged;
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
                string providerName = _initService != null 
                    ? SensorProviderFactory.GetProviderDisplayName(_initService.ConfiguredProviderType)
                    : "Sensors";
                    
                StatusText.Text = status switch
                {
                    AppInitializationService.InitializationStatus.ConnectingDials => "Connecting Dials",
                    AppInitializationService.InitializationStatus.InitializingDials => "Initializing Dials",
                    AppInitializationService.InitializationStatus.ConnectingSensorProvider => $"Connecting {providerName}",
                    AppInitializationService.InitializationStatus.Monitoring => "Monitoring",
                    AppInitializationService.InitializationStatus.Failed => "Initialization Failed",
                    _ => "Unknown Status"
                };

                // Change button color based on status
                UpdateStatusButtonColor(status);
            });
        }

        /// <summary>
        /// Handles sensor provider connection retry status updates.
        /// </summary>
        private void InitService_OnSensorRetryStatusChanged(int retryCount, bool isConnected, int elapsedSeconds)
        {
            Dispatcher.Invoke(() =>
            {
                string providerName = _initService != null 
                    ? SensorProviderFactory.GetProviderDisplayName(_initService.ConfiguredProviderType)
                    : "Sensors";
                    
                StatusText.Text = $"Connecting to {providerName} [{retryCount}]";
                
                if (isConnected)
                {
                    System.Diagnostics.Debug.WriteLine($"[UI] {providerName} connected after {retryCount} attempts in {elapsedSeconds}s");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[UI] {providerName} retry #{retryCount} after {elapsedSeconds}s elapsed");
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
                // Update UI visibility now that we know how many physical dials exist
                if (_initService != null)
                {
                    int physicalDialCount = _initService.GetVU1Controller().DialCount;
                    ApplyDialVisibility(physicalDialCount);
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Updated UI for {physicalDialCount} physical dials");
                    
                    // Update placeholder dial UIDs with actual discovered dial UIDs
                    UpdateDialConfigurationsWithDiscoveredDials();
                }
                
                // Check if this is first run - show settings window instead of starting monitoring
                if (_config?.AppSettings.RunInit == true)
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] RunInit is true - showing settings window for initial setup");
                    ShowInitialSetupWindow();
                    return;
                }
                
                // Normal operation - start monitoring
                StartMonitoring();
                
                // Enable settings button now that monitoring is running
                SettingsButton.IsEnabled = true;
                SettingsButton.ToolTip = "Settings";
                System.Diagnostics.Debug.WriteLine("[MainWindow] Settings button enabled - initialization complete");

                if (_config?.AppSettings.DebugMode == true)
                {
                    var dialCount = _initService?.GetVU1Controller().DialCount ?? 0;
                    var effectiveDialCount = _config?.GetEffectiveDialCount(dialCount) ?? 0;
                    var sensorProvider = _initService?.GetSensorProvider();
                    var sensorConnected = sensorProvider?.IsConnected ?? false;
                    
                    string overrideInfo = _config.AppSettings.DialCountOverride.HasValue 
                        ? $" (Override: {_config.AppSettings.DialCountOverride.Value})" 
                        : "";
                    
                    MessageBox.Show(
                        $"? Initialization Complete\n\n" +
                        $"Physical Dials Detected: {dialCount}\n" +
                        $"Active Dials: {effectiveDialCount}{overrideInfo}\n" +
                        $"Sensor Provider: {sensorProvider?.ProviderName ?? "None"}\n" +
                        $"Sensors Connected: {sensorConnected}",
                        "Debug: Ready",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            });
        }

        /// <summary>
        /// Updates the dial configurations with actual discovered dial UIDs.
        /// This replaces placeholder UIDs with real device UIDs from discovery.
        /// </summary>
        private void UpdateDialConfigurationsWithDiscoveredDials()
        {
            if (_config == null || _initService == null)
                return;

            var vu1 = _initService.GetVU1Controller();
            var discoveredDials = vu1.GetAllDials();
            
            if (discoveredDials.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] No dials discovered - keeping placeholder configuration");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[MainWindow] Updating config with {discoveredDials.Count} discovered dials");

            // Get discovered dial UIDs in order
            var discoveredUids = discoveredDials.Keys.ToList();
            
            // Update each configuration entry with the corresponding discovered dial UID
            for (int i = 0; i < Math.Min(_config.Dials.Count, discoveredUids.Count); i++)
            {
                var dialConfig = _config.Dials[i];
                var discoveredUid = discoveredUids[i];
                
                // Only update if current UID is a placeholder
                if (dialConfig.DialUid.StartsWith("PLACEHOLDER_"))
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Updating dial {i + 1}: {dialConfig.DialUid} -> {discoveredUid}");
                    dialConfig.DialUid = discoveredUid;
                }
            }

            // Limit config to actual discovered dials if fewer were found
            if (discoveredDials.Count < _config.Dials.Count)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Trimming config from {_config.Dials.Count} to {discoveredDials.Count} dials");
                _config.Dials = _config.Dials.Take(discoveredDials.Count).ToList();
            }

            // Save updated configuration
            try
            {
                string configPath = ConfigManager.GetDefaultConfigPath();
                var manager = new ConfigManager(configPath);
                manager.Save(_config);
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Updated configuration saved with discovered dial UIDs");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Failed to save updated configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows the settings window for initial setup after dial discovery.
        /// </summary>
        private async void ShowInitialSetupWindow()
        {
            if (_config == null)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] ERROR: Cannot show setup window - config is null");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine("[MainWindow] Opening settings window for initial setup");
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Config has {_config.Dials.Count} dials:");
            foreach (var dial in _config.Dials)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow]   - {dial.DisplayName}: UID={dial.DialUid}");
            }
            
            // Update status to indicate setup is needed
            StatusText.Text = "Initial Setup Required";
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 0)); // Yellow
            
            // Create settings window with the current configuration (already has discovered dial UIDs)
            var settingsWindow = new SettingsWindow(_config);
            settingsWindow.Owner = this;
            settingsWindow.SetFirstRunMode(true);
            
            // Pass controllers to the settings window
            if (_initService != null)
            {
                // Use the sensor provider abstraction
                var sensorProvider = _initService.GetSensorProvider();
                if (sensorProvider != null)
                {
                    settingsWindow.SetSensorProvider(sensorProvider);
                }
                
                if (_initService.GetVU1Controller() != null)
                {
                    settingsWindow.SetVU1Controller(_initService.GetVU1Controller());
                }
            }
            
            // Show settings window as modal dialog
            var result = settingsWindow.ShowDialog();
            
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Settings window closed with result: {result}");
            
            // After settings window closes, reload config and start monitoring
            if (result == true)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Initial setup complete - reloading config and starting monitoring");
                
                // Reload configuration (it was saved by settings window)
                LoadConfiguration();
                
                // Show dials and hide welcome panel after initial setup is complete
                DialsPanel.Visibility = Visibility.Visible;
                WelcomePanel.Visibility = Visibility.Collapsed;
                
                // CRITICAL: Re-register dial mappings with the new sensor configuration
                // The initial registration had empty sensor names since user hadn't configured yet
                RegisterDialMappingsFromConfig();
                
                // Set initial backlight colors based on configuration
                await SetInitialDialBacklightsAsync();
                
                // Now start monitoring
                StartMonitoring();
                
                // Enable settings button
                SettingsButton.IsEnabled = true;
                SettingsButton.ToolTip = "Settings";
                
                // Update status
                StatusText.Text = "Monitoring";
                StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0, 200, 0)); // Green
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Initial setup was cancelled or closed");
                StatusText.Text = "Setup Incomplete";
                StatusText.Foreground = new SolidColorBrush(WpfColors.Red);
            }
        }

        /// <summary>
        /// Sets initial backlight colors for all dials based on configuration.
        /// Called after initial setup to apply the configured colors.
        /// </summary>
        private async Task SetInitialDialBacklightsAsync()
        {
            if (_config == null || _initService == null)
                return;

            var vu1 = _initService.GetVU1Controller();
            if (vu1 == null || !vu1.IsConnected)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Cannot set backlights - VU1 not connected");
                return;
            }

            var activeDials = _config.GetActiveDials();
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Setting initial backlight colors for {activeDials.Count} dials");

            foreach (var dialConfig in activeDials.Where(d => d.Enabled))
            {
                try
                {
                    // Determine the color to set based on color mode
                    string colorName;
                    if (dialConfig.ColorConfig.ColorMode == "static")
                    {
                        colorName = dialConfig.ColorConfig.StaticColor;
                    }
                    else if (dialConfig.ColorConfig.ColorMode == "off")
                    {
                        colorName = "Off";
                    }
                    else
                    {
                        // For threshold mode, start with normal color
                        colorName = dialConfig.ColorConfig.NormalColor;
                    }

                    var color = GetNamedColorByName(colorName);
                    if (color != null)
                    {
                        bool success = await vu1.SetBacklightColorAsync(dialConfig.DialUid, color);
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Set {dialConfig.DisplayName} backlight to {colorName}: {(success ? "OK" : "FAILED")}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Error setting backlight for {dialConfig.DisplayName}: {ex.Message}");
                }
            }
        }

        private VULib.NamedColor? GetNamedColorByName(string colorName)
        {
            return colorName switch
            {
                "Red" => VULib.Colors.Red,
                "Green" => VULib.Colors.Green,
                "Blue" => VULib.Colors.Blue,
                "Yellow" => VULib.Colors.Yellow,
                "Cyan" => VULib.Colors.Cyan,
                "Magenta" => VULib.Colors.Magenta,
                "Orange" => VULib.Colors.Orange,
                "Purple" => VULib.Colors.Purple,
                "Pink" => VULib.Colors.Pink,
                "White" => VULib.Colors.White,
                "Off" => VULib.Colors.Off,
                _ => null
            };
        }

        /// <summary>
        /// Registers dial-to-sensor mappings with the DialMappingService.
        /// This is needed after initial setup when sensor names are configured for the first time.
        /// </summary>
        private void RegisterDialMappingsFromConfig()
        {
            if (_config == null || _initService == null)
                return;

            IDialMappingService? mappingService = null;
            
            try
            {
                mappingService = _initService.GetDialMappingService();
            }
            catch (InvalidOperationException)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Mapping service not yet initialized");
                return;
            }

            // Clear existing mappings
            mappingService?.ClearAllMappings();

            var activeDials = _config.GetActiveDials();
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Registering {activeDials.Count} dial mappings");

            foreach (var dialConfig in activeDials.Where(d => d.Enabled))
            {
                var mapping = new DialSensorMapping
                {
                    Id = dialConfig.DialUid,
                    SensorName = dialConfig.SensorName,
                    SensorId = dialConfig.SensorId,
                    SensorInstance = dialConfig.SensorInstance,
                    EntryName = dialConfig.EntryName,
                    EntryId = dialConfig.EntryId,
                    MinValue = dialConfig.MinValue,
                    MaxValue = dialConfig.MaxValue,
                    WarningThreshold = dialConfig.WarningThreshold,
                    CriticalThreshold = dialConfig.CriticalThreshold,
                    DisplayName = dialConfig.DisplayName
                };

                mappingService?.RegisterMapping(mapping);
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Registered: {dialConfig.DisplayName} -> {dialConfig.SensorName}/{dialConfig.EntryName}");
            }

            System.Diagnostics.Debug.WriteLine("[MainWindow] Dial mappings registered successfully");
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

                // Log diagnostics if debug mode is enabled
                if (_config.AppSettings.DebugMode)
                {
                    var sensorProvider = _initService.GetSensorProvider();
                    if (sensorProvider != null)
                    {
                        var diagnostics = new DiagnosticsService(sensorProvider);
                        var report = diagnostics.GetDiagnosticsReport();
                        System.Diagnostics.Debug.WriteLine(report);
                    }
                    System.Diagnostics.Debug.WriteLine("=== Starting Monitoring Service ===");
                }

                // Create and start monitoring service using IDialMappingService
                IDialMappingService mappingService;
                try
                {
                    mappingService = _initService.GetDialMappingService();
                }
                catch (InvalidOperationException)
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] Cannot start monitoring - dial mapping service not available");
                    return;
                }

                _monitoringService = new SensorMonitoringService(vu1, mappingService, _config);
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
                    $"Failed to start monitoring: {ex.Message}\n\n{ex.StackTrace}",
                    "Monitoring Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                
                System.Diagnostics.Debug.WriteLine($"Error starting monitoring: {ex.Message}");
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

                // Check if this dial is in the active set and find its position
                var activeDials = _config.GetActiveDials();
                int dialIndex = activeDials.FindIndex(d => d.DialUid == dialUid);
                
                if (dialIndex < 0)
                    return;

                // Map dial position (0-3) to UI elements
                var dialPanelsByPosition = new (Border panel, TextBlock percentage, TextBlock displayName)[]
                {
                    (Dial1Button, Dial1Percentage, Dial1DisplayName),
                    (Dial2Button, Dial2Percentage, Dial2DisplayName),
                    (Dial3Button, Dial3Percentage, Dial3DisplayName),
                    (Dial4Button, Dial4Percentage, Dial4DisplayName)
                };

                if (dialIndex >= dialPanelsByPosition.Length)
                    return;

                var (dialPanel, percentageBlock, displayNameBlock) = dialPanelsByPosition[dialIndex];
                var dialConfig = activeDials[dialIndex];
                
                string displayValue = dialConfig.DisplayFormat == "value"
                    ? $"{update.SensorValue.ToString($"F{dialConfig.DecimalPlaces}")}{dialConfig.DisplayUnit}"
                    : $"{update.DialPercentage}%";

                percentageBlock.Text = displayValue;
                displayNameBlock.Text = update.DisplayName;

                Color panelColor = Color.FromRgb(204, 204, 204);
                Color textColor = WpfColors.Black;
                Color subtextColor = Color.FromRgb(102, 102, 102);

                if (dialConfig.ColorConfig.ColorMode == "off")
                {
                    panelColor = Color.FromRgb(204, 204, 204);
                }
                else if (dialConfig.ColorConfig.ColorMode == "static")
                {
                    panelColor = GetColorFromString(dialConfig.ColorConfig.StaticColor);
                }
                else
                {
                    if (update.IsCritical)
                        panelColor = GetColorFromString(dialConfig.ColorConfig.CriticalColor);
                    else if (update.IsWarning)
                        panelColor = GetColorFromString(dialConfig.ColorConfig.WarningColor);
                    else
                        panelColor = GetColorFromString(dialConfig.ColorConfig.NormalColor);
                }

                textColor = GetContrastingTextColor(panelColor);
                subtextColor = GetContrastingSubtextColor(panelColor);

                dialPanel.Background = new SolidColorBrush(panelColor);
                percentageBlock.Foreground = new SolidColorBrush(textColor);
                displayNameBlock.Foreground = new SolidColorBrush(subtextColor);
            });
        }

        private Color GetContrastingTextColor(Color backgroundColor)
        {
            double luminance = (0.299 * backgroundColor.R + 0.587 * backgroundColor.G + 0.114 * backgroundColor.B) / 255;
            return luminance > 0.5 ? WpfColors.Black : WpfColors.White;
        }

        private Color GetContrastingSubtextColor(Color backgroundColor)
        {
            double luminance = (0.299 * backgroundColor.R + 0.587 * backgroundColor.G + 0.114 * backgroundColor.B) / 255;
            return luminance > 0.5 ? Color.FromRgb(102, 102, 102) : Color.FromRgb(200, 200, 200);
        }

        private void MonitoringService_OnError(string errorMessage)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"Error: {errorMessage}";
                StatusText.Foreground = new SolidColorBrush(WpfColors.Red);
            });
        }

        private void UpdateStatusButtonColor(AppInitializationService.InitializationStatus status)
        {
            Color textColor = status switch
            {
                AppInitializationService.InitializationStatus.ConnectingDials => Color.FromRgb(255, 200, 0),
                AppInitializationService.InitializationStatus.InitializingDials => Color.FromRgb(255, 200, 0),
                AppInitializationService.InitializationStatus.ConnectingSensorProvider => Color.FromRgb(255, 200, 0),
                AppInitializationService.InitializationStatus.Monitoring => Color.FromRgb(0, 200, 0),
                AppInitializationService.InitializationStatus.Failed => WpfColors.Red,
                _ => Color.FromRgb(200, 200, 200)
            };
            StatusText.Foreground = new SolidColorBrush(textColor);
        }

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
                _ => Color.FromRgb(204, 204, 204)
            };
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
                DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        public async Task<bool> ReloadConfiguration()
        {
            try
            {
                var newConfig = ConfigManager.LoadDefault();
                if (newConfig == null || !newConfig.Validate(out var errors))
                    return false;

                var oldConfig = _config;
                var changes = oldConfig != null 
                    ? ConfigurationChangeDetector.DetectChanges(oldConfig, newConfig)
                    : ConfigChangeType.All;

                if (changes == ConfigChangeType.None)
                    return true;

                System.Diagnostics.Debug.WriteLine($"[MainWindow] ReloadConfiguration - detected changes: {changes}");

                if (changes.HasFlag(ConfigChangeType.SensorMappings))
                    await ReloadSensorMappings(newConfig);

                if (changes.HasFlag(ConfigChangeType.UpdateIntervals))
                    ApplyIntervalChanges(newConfig);

                // Handle dial settings changes (includes color changes)
                if (changes.HasFlag(ConfigChangeType.DialSettings))
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] Applying dial settings changes (colors, thresholds, etc.)");
                    _monitoringService?.UpdateConfiguration(newConfig);
                    
                    // Apply physical dial backlight colors
                    await ApplyDialBacklightColorsAsync(newConfig);
                    
                    // Update UI dial panel colors immediately
                    UpdateUIDialPanelColors(newConfig);
                }

                _config = newConfig;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] ReloadConfiguration error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Updates the UI dial panel colors based on the current configuration.
        /// Called when color settings change to immediately reflect changes in the UI.
        /// </summary>
        private void UpdateUIDialPanelColors(DialsConfiguration config)
        {
            var dialPanelsByPosition = new (Border panel, TextBlock percentage, TextBlock displayName)[]
            {
                (Dial1Button, Dial1Percentage, Dial1DisplayName),
                (Dial2Button, Dial2Percentage, Dial2DisplayName),
                (Dial3Button, Dial3Percentage, Dial3DisplayName),
                (Dial4Button, Dial4Percentage, Dial4DisplayName)
            };

            var activeDials = config.GetActiveDials();
            
            for (int i = 0; i < Math.Min(activeDials.Count, dialPanelsByPosition.Length); i++)
            {
                var dialConfig = activeDials[i];
                var (dialPanel, percentageBlock, displayNameBlock) = dialPanelsByPosition[i];

                // Determine color based on color mode
                Color panelColor;
                if (dialConfig.ColorConfig.ColorMode == "off")
                {
                    panelColor = Color.FromRgb(204, 204, 204); // Light gray
                }
                else if (dialConfig.ColorConfig.ColorMode == "static")
                {
                    panelColor = GetColorFromString(dialConfig.ColorConfig.StaticColor);
                }
                else
                {
                    // For threshold mode, use normal color (actual value will update it)
                    panelColor = GetColorFromString(dialConfig.ColorConfig.NormalColor);
                }

                var textColor = GetContrastingTextColor(panelColor);
                var subtextColor = GetContrastingSubtextColor(panelColor);

                dialPanel.Background = new SolidColorBrush(panelColor);
                percentageBlock.Foreground = new SolidColorBrush(textColor);
                displayNameBlock.Foreground = new SolidColorBrush(subtextColor);
            }
            
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Updated UI dial panel colors for {activeDials.Count} dials");
        }

        /// <summary>
        /// Applies backlight colors to physical dials based on configuration.
        /// </summary>
        private async Task ApplyDialBacklightColorsAsync(DialsConfiguration config)
        {
            if (_initService == null)
                return;

            var vu1 = _initService.GetVU1Controller();
            if (vu1 == null || !vu1.IsConnected)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Cannot apply backlight colors - VU1 not connected");
                return;
            }

            var activeDials = config.GetActiveDials();
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Applying backlight colors to {activeDials.Count} dials");

            foreach (var dialConfig in activeDials.Where(d => d.Enabled))
            {
                try
                {
                    // Determine the color to set based on color mode
                    string colorName;
                    if (dialConfig.ColorConfig.ColorMode == "static")
                    {
                        colorName = dialConfig.ColorConfig.StaticColor;
                    }
                    else if (dialConfig.ColorConfig.ColorMode == "off")
                    {
                        colorName = "Off";
                    }
                    else
                    {
                        // For threshold mode, use normal color as initial value
                        // The monitoring loop will update based on actual sensor values
                        colorName = dialConfig.ColorConfig.NormalColor;
                    }

                    var color = GetNamedColorByName(colorName);
                    if (color != null)
                    {
                        bool success = await vu1.SetBacklightColorAsync(dialConfig.DialUid, color);
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Applied {dialConfig.DisplayName} backlight to {colorName}: {(success ? "OK" : "FAILED")}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Error applying backlight for {dialConfig.DisplayName}: {ex.Message}");
                }
            }
        }

        private async Task<bool> ReloadSensorMappings(DialsConfiguration newConfig)
        {
            try
            {
                _monitoringService?.Stop();
                await Task.Delay(200);

                IDialMappingService? mappingService = null;
                try
                {
                    mappingService = _initService?.GetDialMappingService();
                    mappingService?.ClearAllMappings();
                }
                catch (InvalidOperationException)
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] Mapping service not available for reload");
                    return false;
                }

                foreach (var dial in newConfig.Dials.Where(d => d.Enabled))
                {
                    var mapping = new DialSensorMapping
                    {
                        Id = dial.DialUid,
                        SensorName = dial.SensorName,
                        SensorId = dial.SensorId,
                        SensorInstance = dial.SensorInstance,
                        EntryName = dial.EntryName,
                        EntryId = dial.EntryId,
                        MinValue = dial.MinValue,
                        MaxValue = dial.MaxValue,
                        WarningThreshold = dial.WarningThreshold,
                        CriticalThreshold = dial.CriticalThreshold,
                        DisplayName = dial.DisplayName
                    };
                    
                    mappingService?.RegisterMapping(mapping);
                }

                _monitoringService?.Start();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ApplyIntervalChanges(DialsConfiguration newConfig)
        {
            _monitoringService?.UpdateConfiguration(newConfig);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            
            if (_initService != null)
            {
                // Use the sensor provider abstraction
                var sensorProvider = _initService.GetSensorProvider();
                if (sensorProvider != null)
                    settingsWindow.SetSensorProvider(sensorProvider);
                if (_initService.GetVU1Controller() != null)
                    settingsWindow.SetVU1Controller(_initService.GetVU1Controller());
            }
            
            settingsWindow.ShowDialog();
        }

        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            var aboutDialog = new AboutDialog();
            aboutDialog.Owner = this;
            aboutDialog.ShowDialog();
        }

        private void InitializeSystemTray()
        {
            if (Application.Current is App app && app.TrayManager != null)
            {
                _trayManager = app.TrayManager;
                _trayManager.Initialize(this);
            }
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (_trayManager == null)
                return;

            if (WindowState == WindowState.Minimized)
            {
                _monitoringService?.SetUIUpdateEnabled(false);
                _trayManager.HideToTray();
                _trayManager.ShowIcon();
            }
            else if (WindowState == WindowState.Normal)
            {
                _monitoringService?.SetUIUpdateEnabled(true);
                _trayManager.HideIcon();
                ShowInTaskbar = true;
            }
        }
    }
}
