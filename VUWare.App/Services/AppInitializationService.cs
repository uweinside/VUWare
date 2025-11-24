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
        /// Event fired when an error occurs during initialization
        /// </summary>
        public event Action<string>? OnError;

        /// <summary>
        /// Event fired when initialization completes successfully
        /// </summary>
        public event Action? OnInitializationComplete;

        public bool IsInitialized { get; private set; }
        public bool IsInitializing { get; private set; }

        public AppInitializationService(DialsConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _vuController = new VU1Controller();
            _hwInfoController = new HWInfo64Controller();
            IsInitialized = false;
            IsInitializing = false;
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
                    bool connected = _vuController.AutoDetectAndConnect();
                    if (!connected)
                    {
                        // Try a short delay and attempt once more
                        Thread.Sleep(500);
                        connected = _vuController.AutoDetectAndConnect();
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
                bool initialized = await _vuController.InitializeAsync();
                if (!initialized || _vuController.DialCount == 0)
                {
                    return false;
                }

                // Set default positions and colors based on config
                var dials = _vuController.GetAllDials();
                foreach (var dialConfig in _config.Dials.Where(d => d.Enabled))
                {
                    // Find matching dial by UID
                    if (dials.TryGetValue(dialConfig.DialUid, out var dial))
                    {
                        try
                        {
                            // Set initial position to 0%
                            await _vuController.SetDialPercentageAsync(dialConfig.DialUid, 0);

                            // Set initial color to normal color
                            var color = new NamedColor(
                                dialConfig.ColorConfig.NormalColor,
                                GetColorComponent(dialConfig.ColorConfig.NormalColor, 'R'),
                                GetColorComponent(dialConfig.ColorConfig.NormalColor, 'G'),
                                GetColorComponent(dialConfig.ColorConfig.NormalColor, 'B')
                            );
                            await _vuController.SetBacklightColorAsync(dialConfig.DialUid, color);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error initializing dial {dialConfig.DialUid}: {ex.Message}");
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing dials: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Connects to HWInfo64 and registers sensor mappings.
        /// </summary>
        private async Task<bool> ConnectHWInfoAsync(CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!_hwInfoController.Connect())
                    {
                        return false;
                    }

                    // Register all enabled dial configurations as sensor mappings
                    foreach (var dialConfig in _config.Dials.Where(d => d.Enabled))
                    {
                        var mapping = new DialSensorMapping
                        {
                            Id = dialConfig.DialUid,
                            SensorName = dialConfig.SensorName,
                            EntryName = dialConfig.EntryName,
                            MinValue = dialConfig.MinValue,
                            MaxValue = dialConfig.MaxValue,
                            WarningThreshold = dialConfig.WarningThreshold,
                            CriticalThreshold = dialConfig.CriticalThreshold,
                            DisplayName = dialConfig.DisplayName
                        };

                        _hwInfoController.RegisterDialMapping(mapping);
                    }

                    // Set polling interval from config
                    _hwInfoController.PollIntervalMs = _config.AppSettings.GlobalUpdateIntervalMs;

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
