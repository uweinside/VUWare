using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace VUWare.Lib
{
    /// <summary>
    /// Main controller for interacting with VU1 Gauge Hub and dials.
    /// Provides high-level API for device discovery, dial control, and status monitoring.
    /// </summary>
    public class VU1Controller : IDisposable
    {
        private readonly SerialPortManager _serialPort;
        private readonly DeviceManager _deviceManager;
        private readonly ImageUpdateQueue _imageQueue;
        private CancellationTokenSource? _periodicUpdateCancellation;
        private Task? _periodicUpdateTask;
        private bool _isInitialized;

        public bool IsConnected => _serialPort?.IsConnected ?? false;
        public bool IsInitialized => _isInitialized;

        public VU1Controller(int commandDelayMs = 50)
        {
            _serialPort = new SerialPortManager();
            _deviceManager = new DeviceManager(_serialPort, commandDelayMs);
            _imageQueue = new ImageUpdateQueue();
            _isInitialized = false;
        }

        /// <summary>
        /// Attempts to auto-detect and connect to the VU1 Gauge Hub.
        /// </summary>
        public bool AutoDetectAndConnect()
        {
            if (IsConnected)
            {
                Disconnect();
            }

            return _serialPort.AutoDetectAndConnect();
        }

        /// <summary>
        /// Connects to the VU1 Gauge Hub using a specific COM port.
        /// </summary>
        public bool Connect(string portName)
        {
            if (IsConnected)
            {
                Disconnect();
            }

            return _serialPort.Connect(portName);
        }

        /// <summary>
        /// Disconnects from the VU1 Gauge Hub.
        /// </summary>
        public void Disconnect()
        {
            StopPeriodicUpdates();
            _serialPort.Disconnect();
            _isInitialized = false;
        }

        /// <summary>
        /// Initializes the controller by discovering dials and starting periodic updates.
        /// Should be called after establishing a connection.
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected to VU1 hub");
            }

            try
            {
                // Discover all dials
                if (!await _deviceManager.DiscoverDialsAsync())
                {
                    return false;
                }

                _isInitialized = true;

                // Start periodic update loop
                StartPeriodicUpdates();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Initialization failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets all discovered dials indexed by UID.
        /// </summary>
        public IReadOnlyDictionary<string, DialState> GetAllDials()
        {
            return _deviceManager.GetAllDials();
        }

        /// <summary>
        /// Gets a dial by its unique ID.
        /// </summary>
        public DialState GetDial(string uid)
        {
#pragma warning disable CS8603
            return _deviceManager.GetDialByUID(uid);
#pragma warning restore CS8603
        }

        /// <summary>
        /// Sets a dial to a specific percentage value (0-100).
        /// </summary>
        public async Task<bool> SetDialPercentageAsync(string uid, byte percentage)
        {
            if (percentage > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(percentage), "Percentage must be 0-100");
            }

            return await _deviceManager.SetDialPercentageAsync(uid, percentage);
        }

        /// <summary>
        /// Sets the backlight color for a dial (RGBW, 0-100% each).
        /// </summary>
        public async Task<bool> SetBacklightAsync(string uid, byte red, byte green, byte blue, byte white = 0)
        {
            return await _deviceManager.SetBacklightAsync(uid, red, green, blue, white);
        }

        /// <summary>
        /// Sets the backlight to a named color.
        /// </summary>
        public async Task<bool> SetBacklightColorAsync(string uid, NamedColor color)
        {
            return await _deviceManager.SetBacklightAsync(uid, color.Red, color.Green, color.Blue, color.White);
        }

        /// <summary>
        /// Updates easing configuration for smooth dial and backlight transitions.
        /// </summary>
        public async Task<bool> SetEasingConfigAsync(string uid, EasingConfig config)
        {
            return await _deviceManager.SetEasingConfigAsync(uid, config);
        }

        /// <summary>
        /// Sets the e-paper display image. Expects a 3600-byte packed buffer (200x144 1-bit). Use ImageProcessor.LoadImageFile.
        /// </summary>
        public async Task<bool> SetDisplayImageAsync(string uid, byte[]? imageData)
        {
            if (imageData == null || imageData.Length == 0)
                throw new ArgumentException("Image data cannot be empty", nameof(imageData));
            if (imageData.Length != ImageProcessor.BYTES_PER_IMAGE)
                throw new ArgumentException($"Image data must be exactly {ImageProcessor.BYTES_PER_IMAGE} bytes (got {imageData.Length})", nameof(imageData));

            try
            {
#pragma warning disable CS8600
                var dial = _deviceManager.GetDialByUID(uid) ?? throw new ArgumentException($"Dial with UID '{uid}' not found");
#pragma warning restore CS8600

                // Clear (white) then origin
                string clearCmd = CommandBuilder.DisplayClear(dial.Index, false);
                string clearResp = await SendCommandAsync(clearCmd, 1500);
                if (!ProtocolHandler.IsSuccessResponse(ProtocolHandler.ParseResponse(clearResp))) return false;

                string gotoCmd = CommandBuilder.DisplayGotoXY(dial.Index, 0, 0);
                string gotoResp = await SendCommandAsync(gotoCmd, 1500);
                if (!ProtocolHandler.IsSuccessResponse(ProtocolHandler.ParseResponse(gotoResp))) return false;

                var chunks = ImageProcessor.ChunkImageData(imageData);
                int c = 0;
                foreach (var chunk in chunks)
                {
                    c++;
                    string imgCmd = CommandBuilder.DisplayImageData(dial.Index, chunk);
                    string imgResp = await SendCommandAsync(imgCmd, 2500);
                    if (!ProtocolHandler.IsSuccessResponse(ProtocolHandler.ParseResponse(imgResp)))
                    {
                        System.Diagnostics.Debug.WriteLine($"Image chunk {c} failed");
                        return false;
                    }
                    await Task.Delay(200); // pacing as per Python implementation
                }

                await Task.Delay(200); // settle before show
                string showCmd = CommandBuilder.DisplayShowImage(dial.Index);
                string showResp = await SendCommandAsync(showCmd, 4000);
                return ProtocolHandler.IsSuccessResponse(ProtocolHandler.ParseResponse(showResp));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set display image: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Starts the periodic update loop for handling queued commands.
        /// Called automatically by InitializeAsync.
        /// </summary>
        private void StartPeriodicUpdates()
        {
            if (_periodicUpdateTask != null && !_periodicUpdateTask.IsCompleted) return;
            _periodicUpdateCancellation = new CancellationTokenSource();
            _periodicUpdateTask = Task.Run(() => PeriodicUpdateLoop(_periodicUpdateCancellation.Token));
        }
        /// <summary>
        /// Stops the periodic update loop.
        /// Called automatically by Disconnect.
        /// </summary>
        private void StopPeriodicUpdates()
        {
            _periodicUpdateCancellation?.Cancel();
            try { _periodicUpdateTask?.Wait(TimeSpan.FromSeconds(5)); } catch { }
        }
        /// <summary>
        /// Periodic update loop that processes image updates.
        /// Now optimized with idle detection - sleeps longer when no work to do.
        /// </summary>
        private async Task PeriodicUpdateLoop(CancellationToken ct)
        {
            try
            {
                int idleCycles = 0;
                const int maxIdleCycles = 5;
                
                while (!ct.IsCancellationRequested && IsConnected)
                {
                    try
                    {
                        bool didWork = false;
                        
                        // Process pending image updates
                        while (_imageQueue.TryGetNextUpdate(out byte idx, out byte[] img))
                        {
                            var dial = _deviceManager.GetDialByIndex(idx);
                            if (dial != null) 
                            {
                                await SetDisplayImageAsync(dial.UID, img);
                                didWork = true;
                            }
                        }

                        // Adaptive delay: sleep longer when idle to reduce CPU usage
                        if (didWork)
                        {
                            idleCycles = 0;
                            await Task.Delay(500, ct);  // Normal update interval
                        }
                        else
                        {
                            idleCycles++;
                            // Gradually increase sleep time when idle (up to 2 seconds)
                            int sleepMs = Math.Min(500 + (idleCycles * 300), 2000);
                            await Task.Delay(sleepMs, ct);
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Periodic update error: {ex.Message}");
                        await Task.Delay(1000, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Periodic loop failure: {ex.Message}");
            }
        }

        /// <summary>
        /// Sends a command directly to the hub (for advanced use).
        /// Now uses true async I/O!
        /// </summary>
        private async Task<string> SendCommandAsync(string command, int timeoutMs, CancellationToken cancellationToken = default) 
            => await _serialPort.SendCommandAsync(command, timeoutMs, cancellationToken);
        
        /// <summary>
        /// Queues an image update for a dial (processed by periodic update loop).
        /// </summary>
#pragma warning disable CS8600
        public void QueueImageUpdate(string uid, byte[] imageData)
        {
            var dial = _deviceManager.GetDialByUID(uid) ?? throw new ArgumentException($"Dial with UID '{uid}' not found");
            _imageQueue.QueueImageUpdate(dial.Index, imageData);
        }
#pragma warning restore CS8600
        
        /// <summary>
        /// Gets the number of dials discovered.
        /// </summary>
        public int DialCount => _deviceManager.GetAllDials().Count;

        public void Dispose()
        {
            StopPeriodicUpdates();
            _deviceManager?.Dispose();
            _serialPort?.Dispose();
        }
    }

    /// <summary>
    /// Predefined named colors for convenient backlight control.
    /// </summary>
    public class NamedColor
    {
        public string Name { get; set; }
        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }
        public byte White { get; set; }

        public NamedColor(string name, byte red, byte green, byte blue, byte white = 0)
        {
            Name = name;
            Red = red;
            Green = green;
            Blue = blue;
            White = white;
        }
    }

    /// <summary>
    /// Predefined color constants for backlight control.
    /// </summary>
    public static class Colors
    {
        public static readonly NamedColor Off = new("Off", 0, 0, 0, 0);
        public static readonly NamedColor Red = new("Red", 100, 0, 0);
        public static readonly NamedColor Green = new("Green", 0, 100, 0);
        public static readonly NamedColor Blue = new("Blue", 0, 0, 100);
        public static readonly NamedColor White = new("White", 100, 100, 100);
        public static readonly NamedColor Yellow = new("Yellow", 100, 100, 0);
        public static readonly NamedColor Cyan = new("Cyan", 0, 100, 100);
        public static readonly NamedColor Magenta = new("Magenta", 100, 0, 100);
        public static readonly NamedColor Orange = new("Orange", 100, 50, 0);
        public static readonly NamedColor Purple = new("Purple", 100, 0, 100);
        public static readonly NamedColor Pink = new("Pink", 100, 25, 50);
    }
}
