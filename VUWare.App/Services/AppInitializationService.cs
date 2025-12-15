using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VUWare.App.Models;
using VUWare.HWInfo64;
using VUWare.Lib;
using VUWare.Lib.Sensors;

namespace VUWare.App.Services
{
    /// <summary>
    /// Manages application initialization including dial connection and sensor provider setup.
    /// All operations run on background threads with UI callbacks for status updates.
    /// </summary>
    public class AppInitializationService : IDisposable
    {
        private readonly VU1Controller _vuController;
        private ISensorProvider? _sensorProvider;
        private IDialMappingService? _dialMappingService;
        private readonly DialsConfiguration _config;
        private CancellationTokenSource? _initializationCts;
        private Task? _initializationTask;
        private bool _disposed;
        private int _sensorRetryCount;
        private int _sensorMaxRetries;

        /// <summary>
        /// Initialization status for UI display
        /// </summary>
        public enum InitializationStatus
        {
            Idle,
            ConnectingDials,
            InitializingDials,
            ConnectingSensorProvider,
            Monitoring,
            Failed
        }

        /// <summary>
        /// Event fired when initialization status changes
        /// </summary>
        public event Action<InitializationStatus>? OnStatusChanged;

        /// <summary>
        /// Event fired when sensor provider connection retry status changes.
        /// Arguments: (retryCount, isConnected, elapsedSeconds)
        /// </summary>
        public event Action<int, bool, int>? OnSensorRetryStatusChanged;

        /// <summary>
        /// Event fired when an error occurs during initialization
        /// </summary>
        public event Action<string>? OnError;

        /// <summary>
        /// Event fired when initialization completes successfully
        /// </summary>
        public event Action? OnInitializationComplete;

        public bool IsInitialized { get; private set; }
        public bool IsInitializing { get; private set; }

        /// <summary>
        /// Gets the current sensor provider retry count (for UI display).
        /// </summary>
        public int SensorRetryCount => _sensorRetryCount;

        /// <summary>
        /// Gets the maximum number of sensor provider retries before timeout (for UI display).
        /// </summary>
        public int SensorMaxRetries => _sensorMaxRetries;

        /// <summary>
        /// Gets the configured sensor provider type.
        /// </summary>
        public SensorProviderType ConfiguredProviderType => _config.AppSettings.SensorProvider;

        public AppInitializationService(DialsConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _vuController = new VU1Controller(_config.AppSettings.SerialCommandDelayMs);
            IsInitialized = false;
            IsInitializing = false;
            _sensorRetryCount = 0;
            _sensorMaxRetries = 300; // 5 minutes at 1 second intervals
        }

        /// <summary>
        /// Starts the asynchronous initialization process on a background thread.
        /// </summary>
        public void StartInitialization()
        {
            if (IsInitializing || IsInitialized)
            {
                return;
            }

            _initializationCts = new CancellationTokenSource();
            _initializationTask = Task.Run(() => InitializationWorker(_initializationCts.Token));
        }

        /// <summary>
        /// Cancels the ongoing initialization.
        /// </summary>
        public void CancelInitialization()
        {
            _initializationCts?.Cancel();
        }

        /// <summary>
        /// Gets the VU1 controller (available after successful initialization).
        /// </summary>
        public VU1Controller GetVU1Controller() => _vuController;

        /// <summary>
        /// Gets the sensor provider abstraction (available after successful initialization).
        /// Use this for provider-agnostic sensor access.
        /// </summary>
        public ISensorProvider? GetSensorProvider() => _sensorProvider;

        /// <summary>
        /// Gets the dial mapping service (available after successful initialization).
        /// Use this for provider-agnostic dial-to-sensor mapping.
        /// </summary>
        public IDialMappingService GetDialMappingService()
        {
            if (_dialMappingService == null)
            {
                throw new InvalidOperationException("Dial mapping service not available. Ensure initialization is complete.");
            }
            return _dialMappingService;
        }

        /// <summary>
        /// Background worker thread that handles initialization steps.
        /// </summary>
        private async void InitializationWorker(CancellationToken cancellationToken)
        {
            try
            {
                IsInitializing = true;

                // Step 1: Connect to VU1 dials
                RaiseStatusChanged(InitializationStatus.ConnectingDials);
                if (cancellationToken.IsCancellationRequested) return;

                bool dialConnected = await ConnectDialsAsync(cancellationToken);
                if (!dialConnected)
                {
                    RaiseError("Failed to connect to VU1 dials. Check USB connection.");
                    RaiseStatusChanged(InitializationStatus.Failed);
                    return;
                }

                // Step 2: Initialize dials (discover devices)
                RaiseStatusChanged(InitializationStatus.InitializingDials);
                if (cancellationToken.IsCancellationRequested) return;

                bool dialsInitialized = await InitializeDialsAsync(cancellationToken);
                if (!dialsInitialized)
                {
                    RaiseError("Failed to initialize dials. Check I2C connections.");
                    RaiseStatusChanged(InitializationStatus.Failed);
                    return;
                }

                // Step 3: Connect to sensor provider and register sensor mappings
                RaiseStatusChanged(InitializationStatus.ConnectingSensorProvider);
                if (cancellationToken.IsCancellationRequested) return;

                string providerName = SensorProviderFactory.GetProviderDisplayName(_config.AppSettings.SensorProvider);
                bool sensorConnected = await ConnectSensorProviderAsync(cancellationToken);
                if (!sensorConnected)
                {
                    RaiseError($"Failed to connect to {providerName}. Continuing without sensor monitoring.");
                    // Don't fail here - sensor provider is optional
                }

                // Step 4: Mark as ready for monitoring
                RaiseStatusChanged(InitializationStatus.Monitoring);
                IsInitialized = true;

                RaiseInitializationComplete();
            }
            catch (OperationCanceledException)
            {
                // Initialization was cancelled
                IsInitializing = false;
                _vuController.Disconnect();
                _sensorProvider?.Disconnect();
            }
            catch (Exception ex)
            {
                RaiseError($"Unexpected error during initialization: {ex.Message}");
                RaiseStatusChanged(InitializationStatus.Failed);
                IsInitializing = false;
                _vuController.Disconnect();
                _sensorProvider?.Disconnect();
            }
            finally
            {
                IsInitializing = false;
            }
        }

        /// <summary>
        /// Connects to the VU1 Gauge Hub.
        /// </summary>
        private async Task<bool> ConnectDialsAsync(CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // CRITICAL: Add startup delay to allow USB device enumeration to complete
                    System.Diagnostics.Debug.WriteLine("[Init] Waiting for USB enumeration to stabilize...");
                    Thread.Sleep(2000);
                    
                    System.Diagnostics.Debug.WriteLine("[Init] Attempting first connection...");
                    bool connected = _vuController.AutoDetectAndConnect();
                    if (!connected)
                    {
                        System.Diagnostics.Debug.WriteLine("[Init] First connection failed, retrying after 1s delay...");
                        Thread.Sleep(1000);
                        connected = _vuController.AutoDetectAndConnect();
                        
                        if (!connected)
                        {
                            System.Diagnostics.Debug.WriteLine("[Init] Second connection failed, final attempt after 2s delay...");
                            Thread.Sleep(2000);
                            connected = _vuController.AutoDetectAndConnect();
                        }
                    }
                    
                    if (connected)
                    {
                        System.Diagnostics.Debug.WriteLine("[Init] ? Successfully connected to VU1 hub");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[Init] ? Failed to connect after all retry attempts");
                    }
                    
                    return connected;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error connecting dials: {ex.Message}");
                    return false;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Initializes the VU1 dials (discovery).
        /// </summary>
        private async Task<bool> InitializeDialsAsync(CancellationToken cancellationToken)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[Init] Starting dial discovery...");
                bool initialized = await _vuController.InitializeAsync().ConfigureAwait(false);
                
                if (!initialized)
                {
                    System.Diagnostics.Debug.WriteLine("[Init] Dial initialization returned false");
                    return false;
                }
                
                if (_vuController.DialCount == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[Init] No dials discovered");
                    return false;
                }

                int physicalDialCount = _vuController.DialCount;
                System.Diagnostics.Debug.WriteLine($"[Init] Discovered {physicalDialCount} physical dial(s)");

                var activeDials = _config.GetActiveDials(physicalDialCount);
                int effectiveCount = _config.GetEffectiveDialCount(physicalDialCount);
                
                System.Diagnostics.Debug.WriteLine($"[Init] Active dials: {effectiveCount} (Physical: {physicalDialCount}, Configured: {_config.Dials.Count})");

                var dials = _vuController.GetAllDials();
                foreach (var dialConfig in activeDials.Where(d => d.Enabled))
                {
                    if (dials.TryGetValue(dialConfig.DialUid, out var dial))
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"[Init] Setting initial state for {dial.Name}");
                            
                            await _vuController.SetDialPercentageAsync(dialConfig.DialUid, 0).ConfigureAwait(false);
                            System.Diagnostics.Debug.WriteLine($"[Init] Set {dial.Name} position to 0%");

                            var color = new NamedColor(
                                dialConfig.ColorConfig.NormalColor,
                                GetColorComponent(dialConfig.ColorConfig.NormalColor, 'R'),
                                GetColorComponent(dialConfig.ColorConfig.NormalColor, 'G'),
                                GetColorComponent(dialConfig.ColorConfig.NormalColor, 'B')
                            );
                            await _vuController.SetBacklightColorAsync(dialConfig.DialUid, color).ConfigureAwait(false);
                            System.Diagnostics.Debug.WriteLine($"[Init] Set {dial.Name} color to {color.Name}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Init] Error initializing dial {dialConfig.DialUid}: {ex.Message}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Init] Warning: Configured dial {dialConfig.DialUid} not found in discovered dials");
                    }
                }

                System.Diagnostics.Debug.WriteLine("[Init] Dial initialization complete");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Init] ERROR in InitializeDialsAsync: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[Init] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Connects to the configured sensor provider with retry logic and registers sensor mappings.
        /// Uses the factory pattern to create the appropriate provider based on configuration.
        /// </summary>
        private async Task<bool> ConnectSensorProviderAsync(CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var providerType = _config.AppSettings.SensorProvider;
                    string providerName = SensorProviderFactory.GetProviderDisplayName(providerType);
                    
                    System.Diagnostics.Debug.WriteLine($"[SensorProvider] Connecting to {providerName}...");

                    // Check if provider is supported
                    if (!SensorProviderFactory.IsProviderSupported(providerType))
                    {
                        System.Diagnostics.Debug.WriteLine($"[SensorProvider] {providerName} is not yet supported");
                        RaiseError($"{providerName} is not yet implemented. {SensorProviderFactory.GetProviderRequirements(providerType)}");
                        return false;
                    }

                    // Retry loop for sensor provider connection
                    _sensorRetryCount = 0;
                    var startTime = DateTime.Now;
                    
                    while (!cancellationToken.IsCancellationRequested && _sensorRetryCount < _sensorMaxRetries)
                    {
                        _sensorRetryCount++;
                        int elapsedSeconds = (int)(DateTime.Now - startTime).TotalSeconds;
                        
                        // Try to create and connect
                        if (SensorProviderFactory.TryCreateAndConnect(providerType, out var provider))
                        {
                            _sensorProvider = provider;
                            
                            System.Diagnostics.Debug.WriteLine($"[SensorProvider] ? Connected to {providerName} after {_sensorRetryCount} attempt(s)");
                            RaiseSensorRetryStatusChanged(_sensorRetryCount, true, elapsedSeconds);
                            
                            // Create the dial mapping service using the provider
                            _dialMappingService = new DialMappingService(_sensorProvider!);

                            // Register dial mappings
                            RegisterDialMappings();
                            
                            return true;
                        }

                        System.Diagnostics.Debug.WriteLine($"[SensorProvider] Attempt {_sensorRetryCount}: {providerName} not available, retrying...");
                        RaiseSensorRetryStatusChanged(_sensorRetryCount, false, elapsedSeconds);
                        
                        // Wait before retry
                        Thread.Sleep(1000);
                    }

                    System.Diagnostics.Debug.WriteLine($"[SensorProvider] ? Failed to connect to {providerName} after {_sensorRetryCount} attempts");
                    return false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SensorProvider] Error: {ex.Message}");
                    return false;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Registers dial-to-sensor mappings with the dial mapping service.
        /// </summary>
        private void RegisterDialMappings()
        {
            if (_dialMappingService == null)
            {
                System.Diagnostics.Debug.WriteLine("[SensorProvider] Cannot register mappings - dial mapping service not initialized");
                return;
            }

            int physicalDialCount = _vuController.DialCount;
            var activeDials = _config.GetActiveDials(physicalDialCount);

            System.Diagnostics.Debug.WriteLine($"[SensorProvider] Registering {activeDials.Count} active dial mappings");

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

                _dialMappingService.RegisterMapping(mapping);
                System.Diagnostics.Debug.WriteLine($"[SensorProvider] Registered mapping for {dialConfig.DisplayName}");
            }
        }

        /// <summary>
        /// Helper method to extract color component from color name.
        /// </summary>
        private static byte GetColorComponent(string colorName, char component)
        {
            var colorMap = new Dictionary<string, (byte R, byte G, byte B)>
            {
                { "Red", (100, 0, 0) },
                { "Green", (0, 100, 0) },
                { "Blue", (0, 0, 100) },
                { "Yellow", (100, 100, 0) },
                { "Cyan", (0, 100, 100) },
                { "Magenta", (100, 0, 100) },
                { "Orange", (100, 50, 0) },
                { "Purple", (100, 0, 100) },
                { "Pink", (100, 25, 50) },
                { "White", (100, 100, 100) },
                { "Off", (0, 0, 0) }
            };

            if (colorMap.TryGetValue(colorName, out var rgb))
            {
                return component switch
                {
                    'R' => rgb.R,
                    'G' => rgb.G,
                    'B' => rgb.B,
                    _ => 0
                };
            }

            return 0;
        }

        private void RaiseStatusChanged(InitializationStatus status)
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    OnStatusChanged?.Invoke(status);
                });
            }
            catch
            {
                OnStatusChanged?.Invoke(status);
            }
        }

        private void RaiseSensorRetryStatusChanged(int retryCount, bool isConnected, int elapsedSeconds)
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    OnSensorRetryStatusChanged?.Invoke(retryCount, isConnected, elapsedSeconds);
                });
            }
            catch
            {
                OnSensorRetryStatusChanged?.Invoke(retryCount, isConnected, elapsedSeconds);
            }
        }

        private void RaiseError(string errorMessage)
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    OnError?.Invoke(errorMessage);
                });
            }
            catch
            {
                OnError?.Invoke(errorMessage);
            }
        }

        private void RaiseInitializationComplete()
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    OnInitializationComplete?.Invoke();
                });
            }
            catch
            {
                OnInitializationComplete?.Invoke();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _initializationCts?.Cancel();
                try { _initializationTask?.Wait(TimeSpan.FromSeconds(5)); } catch { }

                _vuController?.Dispose();
                _sensorProvider?.Dispose();
                _dialMappingService?.Dispose();
                _initializationCts?.Dispose();
                _disposed = true;
            }
        }
    }
}
