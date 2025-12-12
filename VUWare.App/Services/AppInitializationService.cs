using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VUWare.App.Models;
using VUWare.HWInfo64;
using VUWare.Lib;

namespace VUWare.App.Services
{
    /// <summary>
    /// Manages application initialization including dial connection and HWInfo sensor setup.
    /// All operations run on background threads with UI callbacks for status updates.
    /// </summary>
    public class AppInitializationService : IDisposable
    {
        private readonly VU1Controller _vuController;
        private readonly HWInfo64Controller _hwInfoController;
        private readonly DialsConfiguration _config;
        private CancellationTokenSource? _initializationCts;
        private Task? _initializationTask;
        private bool _disposed;
        private int _hwInfoRetryCount;
        private int _hwInfoMaxRetries;

        /// <summary>
        /// Initialization status for UI display
        /// </summary>
        public enum InitializationStatus
        {
            Idle,
            ConnectingDials,
            InitializingDials,
            ConnectingHWInfo,
            Monitoring,
            Failed
        }

        /// <summary>
        /// Event fired when initialization status changes
        /// </summary>
        public event Action<InitializationStatus>? OnStatusChanged;

        /// <summary>
        /// Event fired when HWInfo connection retry status changes.
        /// Arguments: (retryCount, isConnected, elapsedSeconds)
        /// </summary>
        public event Action<int, bool, int>? OnHWInfoRetryStatusChanged;

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
        /// Gets the current HWInfo retry count (for UI display).
        /// </summary>
        public int HWInfoRetryCount => _hwInfoRetryCount;

        /// <summary>
        /// Gets the maximum number of HWInfo retries before timeout (for UI display).
        /// </summary>
        public int HWInfoMaxRetries => _hwInfoMaxRetries;

        public AppInitializationService(DialsConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _vuController = new VU1Controller(_config.AppSettings.SerialCommandDelayMs);
            _hwInfoController = new HWInfo64Controller();
            IsInitialized = false;
            IsInitializing = false;
            _hwInfoRetryCount = 0;
            _hwInfoMaxRetries = 300; // 5 minutes at 1 second intervals
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
        /// Gets the HWInfo64 controller (available after successful initialization).
        /// </summary>
        public HWInfo64Controller GetHWInfo64Controller() => _hwInfoController;

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

                // Step 3: Connect to HWInfo64 and register sensor mappings
                RaiseStatusChanged(InitializationStatus.ConnectingHWInfo);
                if (cancellationToken.IsCancellationRequested) return;

                bool hwInfoConnected = await ConnectHWInfoAsync(cancellationToken);
                if (!hwInfoConnected)
                {
                    RaiseError("Failed to connect to HWInfo64. Continuing without sensor monitoring.");
                    // Don't fail here - HWInfo is optional
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
                _hwInfoController.Disconnect();
            }
            catch (Exception ex)
            {
                RaiseError($"Unexpected error during initialization: {ex.Message}");
                RaiseStatusChanged(InitializationStatus.Failed);
                IsInitializing = false;
                _vuController.Disconnect();
                _hwInfoController.Disconnect();
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
                    // When app launches at Windows startup or via Explorer, USB devices may not be
                    // fully enumerated yet. VS debugger adds natural delays that mask this issue.
                    System.Diagnostics.Debug.WriteLine("[Init] Waiting for USB enumeration to stabilize...");
                    Thread.Sleep(2000); // 2 second startup delay
                    
                    System.Diagnostics.Debug.WriteLine("[Init] Attempting first connection...");
                    bool connected = _vuController.AutoDetectAndConnect();
                    if (!connected)
                    {
                        // Try a short delay and attempt once more
                        System.Diagnostics.Debug.WriteLine("[Init] First connection failed, retrying after 1s delay...");
                        Thread.Sleep(1000);
                        connected = _vuController.AutoDetectAndConnect();
                        
                        if (!connected)
                        {
                            // One final attempt with longer delay
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

                System.Diagnostics.Debug.WriteLine($"[Init] Discovered {_vuController.DialCount} dial(s)");

                // Get active dials based on effective dial count
                var activeDials = _config.GetActiveDials();
                int effectiveCount = _config.GetEffectiveDialCount();
                
                System.Diagnostics.Debug.WriteLine($"[Init] Active dials: {effectiveCount} (out of {_config.Dials.Count} configured)");

                // Set default positions and colors based on config
                var dials = _vuController.GetAllDials();
                foreach (var dialConfig in activeDials.Where(d => d.Enabled))
                {
                    // Find matching dial by UID
                    if (dials.TryGetValue(dialConfig.DialUid, out var dial))
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"[Init] Setting initial state for {dial.Name}");
                            
                            // Set initial position to 0% - ConfigureAwait(false) prevents deadlock
                            await _vuController.SetDialPercentageAsync(dialConfig.DialUid, 0).ConfigureAwait(false);
                            System.Diagnostics.Debug.WriteLine($"[Init] Set {dial.Name} position to 0%");

                            // Set initial color to normal color
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
                            // Continue with other dials even if one fails
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
        /// Connects to HWInfo64 with retry logic and registers sensor mappings.
        /// </summary>
        private async Task<bool> ConnectHWInfoAsync(CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Create a new reader that will be used for initialization
                    var hwInfoReader = new HWiNFOReader();
                    
                    // Create initialization service with retry logic
                    var hwInfoInit = new HWiNFOInitializationService(
                        reader: hwInfoReader,
                        retryIntervalMs: 1000,      // Retry every 1 second
                        maxTimeoutMs: 300000        // Timeout after 5 minutes
                    );

                    // Subscribe to retry status updates
                    hwInfoInit.OnStatusChanged += (retryCount, isConnected, elapsedMs) =>
                    {
                        _hwInfoRetryCount = retryCount;
                        int elapsedSeconds = elapsedMs / 1000;
                        System.Diagnostics.Debug.WriteLine($"[HWInfo] Retry {retryCount}: Connected={isConnected}, Elapsed={elapsedSeconds}s");
                        RaiseHWInfoRetryStatusChanged(retryCount, isConnected, elapsedSeconds);
                    };

                    // Synchronously initialize (blocks until connection or timeout)
                    bool connected = hwInfoInit.InitializeSync(cancellationToken);

                    if (!connected)
                    {
                        System.Diagnostics.Debug.WriteLine($"[HWInfo] Failed to connect after {_hwInfoRetryCount} attempts");
                        hwInfoInit.Dispose();
                        return false;
                    }

                    // Get active dials based on effective dial count
                    var activeDials = _config.GetActiveDials();
                    
                    System.Diagnostics.Debug.WriteLine($"[HWInfo] Registering {activeDials.Count} active dial mappings");

                    // Register all enabled active dial configurations as sensor mappings
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

                        _hwInfoController.RegisterDialMapping(mapping);
                        System.Diagnostics.Debug.WriteLine($"[HWInfo] Registered mapping for {dialConfig.DisplayName}");
                    }

                    // Set polling interval from config
                    _hwInfoController.PollIntervalMs = _config.AppSettings.GlobalUpdateIntervalMs;
                    
                    // Start polling with the successfully connected reader
                    _hwInfoController.ConnectWithReader(hwInfoReader);

                    System.Diagnostics.Debug.WriteLine($"[HWInfo] Successfully connected and configured after {_hwInfoRetryCount} attempts");
                    
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error connecting to HWInfo64: {ex.Message}");
                    return false;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Helper method to extract color component from color name.
        /// Returns percentage value (0-100) for the specified color channel.
        /// </summary>
        private static byte GetColorComponent(string colorName, char component)
        {
            // Map color names to RGB values
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

        /// <summary>
        /// Raises the status changed event on the UI thread.
        /// </summary>
        private void RaiseStatusChanged(InitializationStatus status)
        {
            // Use Application.Current.Dispatcher if in WPF context
            try
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    OnStatusChanged?.Invoke(status);
                });
            }
            catch
            {
                // Fallback if dispatcher not available
                OnStatusChanged?.Invoke(status);
            }
        }

        /// <summary>
        /// Raises the HWInfo retry status changed event on the UI thread.
        /// </summary>
        private void RaiseHWInfoRetryStatusChanged(int retryCount, bool isConnected, int elapsedSeconds)
        {
            try
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    OnHWInfoRetryStatusChanged?.Invoke(retryCount, isConnected, elapsedSeconds);
                });
            }
            catch
            {
                OnHWInfoRetryStatusChanged?.Invoke(retryCount, isConnected, elapsedSeconds);
            }
        }

        /// <summary>
        /// Raises the error event on the UI thread.
        /// </summary>
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

        /// <summary>
        /// Raises the initialization complete event on the UI thread.
        /// </summary>
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
                _hwInfoController?.Dispose();
                _initializationCts?.Dispose();
                _disposed = true;
            }
        }
    }
}
