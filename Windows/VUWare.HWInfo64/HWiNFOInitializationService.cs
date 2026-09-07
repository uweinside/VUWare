using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace VUWare.HWInfo64
{
    /// <summary>
    /// Handles initialization of HWInfo64 shared memory with retry logic.
    /// Supports non-blocking async initialization with progress callbacks.
    /// </summary>
    public class HWiNFOInitializationService : IDisposable
    {
        private readonly HWiNFOReader _reader;
        private readonly int _retryIntervalMs;
        private readonly int _maxTimeoutMs;
        private bool _disposed;

        /// <summary>
        /// Event fired when initialization status changes.
        /// Arguments: (retryCount, isConnected, elapsedMs)
        /// </summary>
        public event Action<int, bool, int>? OnStatusChanged;

        /// <summary>
        /// Event fired when initialization completes (success or timeout).
        /// Arguments: (isConnected, totalRetries, elapsedMs)
        /// </summary>
        public event Action<bool, int, int>? OnInitializationComplete;

        /// <summary>
        /// Gets the current connection status.
        /// </summary>
        public bool IsConnected => _reader.IsConnected;

        /// <summary>
        /// Gets the HWiNFOReader instance.
        /// </summary>
        public HWiNFOReader Reader => _reader;

        /// <summary>
        /// Creates a new initialization service with default retry settings.
        /// Default: 1 second retry interval, 5 minute timeout
        /// </summary>
        public HWiNFOInitializationService()
            : this(new HWiNFOReader(), retryIntervalMs: 1000, maxTimeoutMs: 300000)
        {
        }

        /// <summary>
        /// Creates a new initialization service with custom retry settings.
        /// </summary>
        /// <param name="reader">HWiNFOReader instance</param>
        /// <param name="retryIntervalMs">Time between retry attempts (minimum 100ms)</param>
        /// <param name="maxTimeoutMs">Maximum time to retry before giving up (minimum 5 seconds)</param>
        public HWiNFOInitializationService(HWiNFOReader reader, int retryIntervalMs = 1000, int maxTimeoutMs = 300000)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            
            // Validate and constrain parameters
            _retryIntervalMs = Math.Max(100, retryIntervalMs);
            _maxTimeoutMs = Math.Max(5000, maxTimeoutMs);
        }

        /// <summary>
        /// Starts asynchronous initialization with retry logic.
        /// Does not block; calls OnStatusChanged and OnInitializationComplete events instead.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Task that completes when initialization is done or cancelled</returns>
        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() => InitializationWorker(cancellationToken));
        }

        /// <summary>
        /// Synchronous initialization with retry logic.
        /// Blocks until connection succeeds or timeout is reached.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>True if successfully connected, false if timeout or cancelled</returns>
        public bool InitializeSync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            int retryCount = 0;

            while (stopwatch.ElapsedMilliseconds < _maxTimeoutMs)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine($"[HWiNFOInit] Initialization cancelled after {retryCount} attempts");
                    OnInitializationComplete?.Invoke(false, retryCount, (int)stopwatch.ElapsedMilliseconds);
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[HWiNFOInit] Attempt {retryCount + 1}: Connecting to HWInfo64...");
                
                if (_reader.Connect())
                {
                    stopwatch.Stop();
                    System.Diagnostics.Debug.WriteLine($"[HWiNFOInit] Successfully connected after {retryCount + 1} attempts in {stopwatch.ElapsedMilliseconds}ms");
                    OnStatusChanged?.Invoke(retryCount + 1, true, (int)stopwatch.ElapsedMilliseconds);
                    OnInitializationComplete?.Invoke(true, retryCount + 1, (int)stopwatch.ElapsedMilliseconds);
                    return true;
                }

                retryCount++;
                OnStatusChanged?.Invoke(retryCount, false, (int)stopwatch.ElapsedMilliseconds);

                // Wait before retry, but check cancellation token
                try
                {
                    Task.Delay(_retryIntervalMs, cancellationToken).Wait(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine($"[HWiNFOInit] Initialization cancelled after {retryCount} attempts");
                    OnInitializationComplete?.Invoke(false, retryCount, (int)stopwatch.ElapsedMilliseconds);
                    return false;
                }
            }

            stopwatch.Stop();
            System.Diagnostics.Debug.WriteLine($"[HWiNFOInit] Initialization timeout after {retryCount} attempts ({stopwatch.ElapsedMilliseconds}ms)");
            OnInitializationComplete?.Invoke(false, retryCount, (int)stopwatch.ElapsedMilliseconds);
            return false;
        }

        /// <summary>
        /// Background worker for asynchronous initialization.
        /// </summary>
        private void InitializationWorker(CancellationToken cancellationToken)
        {
            try
            {
                InitializeSync(cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HWiNFOInit] Unexpected error during initialization: {ex.Message}");
            }
        }

        /// <summary>
        /// Disconnects from HWInfo64.
        /// </summary>
        public void Disconnect()
        {
            _reader?.Disconnect();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _reader?.Dispose();
                _disposed = true;
            }
        }
    }
}
