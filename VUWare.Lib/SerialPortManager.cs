using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace VUWare.Lib
{
    /// <summary>
    /// Manages serial port communication with the VU1 Gauge Hub.
    /// Handles USB device detection, port configuration, and thread-safe async communication.
    /// </summary>
    public class SerialPortManager : IDisposable
    {
        private const string VU1_USB_VID = "0403";
        private const string VU1_USB_PID = "6015";
        private const int BAUD_RATE = 115200;
        private const int READ_TIMEOUT_MS = 2000;
        private const int WRITE_TIMEOUT_MS = 2000;
        private const int RESPONSE_BUFFER_SIZE = 10000;

        private SerialPort? _serialPort;
        private readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1, 1);
        private CancellationTokenSource? _disconnectCts;
        private bool _isConnected;
        private bool _disposed;

        public bool IsConnected => _isConnected;

        /// <summary>
        /// Attempts to connect to the VU1 Gauge Hub by auto-detecting the USB port.
        /// </summary>
        public bool AutoDetectAndConnect()
        {
            try
            {
#pragma warning disable CS8600
                string portName = FindVU1Port();
#pragma warning restore CS8600
                if (string.IsNullOrEmpty(portName))
                {
                    return false;
                }

                return Connect(portName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-detect failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Connects to the VU1 hub using a specified port name.
        /// </summary>
        public bool Connect(string portName)
        {
            _asyncLock.Wait();
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                }

                _serialPort = new SerialPort(portName)
                {
                    BaudRate = BAUD_RATE,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    ReadTimeout = READ_TIMEOUT_MS,
                    WriteTimeout = WRITE_TIMEOUT_MS,
                    Handshake = Handshake.None
                };

                _serialPort.Open();
                
                // Reset cancellation token for new connection
                _disconnectCts?.Cancel();
                _disconnectCts?.Dispose();
                _disconnectCts = new CancellationTokenSource();
                
                _isConnected = true;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Connection failed: {ex.Message}");
                _isConnected = false;
                return false;
            }
            finally
            {
                _asyncLock.Release();
            }
        }

        /// <summary>
        /// Disconnects from the VU1 hub.
        /// </summary>
        public void Disconnect()
        {
            // Signal all pending operations to cancel first
            _isConnected = false;
            _disconnectCts?.Cancel();
            
            // Try to acquire lock with timeout to prevent indefinite hang
            bool acquiredLock = _asyncLock.Wait(TimeSpan.FromSeconds(2));
            
            if (!acquiredLock)
            {
                System.Diagnostics.Debug.WriteLine("[SerialPort] Warning: Disconnect timed out waiting for lock, forcing disconnect");
            }
            
            try
            {
                if (_serialPort != null)
                {
                    try
                    {
                        if (_serialPort.IsOpen)
                        {
                            _serialPort.Close();
                        }
                        _serialPort.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SerialPort] Error disposing serial port: {ex.Message}");
                    }
                    _serialPort = null;
                }
            }
            finally
            {
                // Only release if we successfully acquired the lock
                if (acquiredLock)
                {
                    try { _asyncLock.Release(); } catch { }
                }
            }
        }

        /// <summary>
        /// Sends a command to the hub and waits for a response asynchronously.
        /// Thread-safe; only one command can be in-flight at a time.
        /// </summary>
        public async Task<string> SendCommandAsync(string command, int timeoutMs = READ_TIMEOUT_MS, CancellationToken cancellationToken = default)
        {
            // Link caller's cancellation token with disconnect cancellation
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disconnectCts?.Token ?? CancellationToken.None);
            
            await _asyncLock.WaitAsync(linkedCts.Token).ConfigureAwait(false);
            
            // Capture the serial port reference while holding the lock to prevent it from becoming null
            SerialPort? portSnapshot = _serialPort;
            
            try
            {
                if (!_isConnected || portSnapshot == null || !portSnapshot.IsOpen)
                {
                    throw new InvalidOperationException("Serial port is not connected");
                }

                // Clear any pending data
                if (portSnapshot.BytesToRead > 0)
                {
                    portSnapshot.DiscardInBuffer();
                }

                // Send command with CRLF terminator
                System.Diagnostics.Debug.WriteLine($"[SerialPort] Sending command: {command}");
                
                byte[] commandBytes = Encoding.ASCII.GetBytes(command + "\r\n");
                await portSnapshot.BaseStream.WriteAsync(commandBytes, 0, commandBytes.Length, linkedCts.Token).ConfigureAwait(false);
                await portSnapshot.BaseStream.FlushAsync(linkedCts.Token).ConfigureAwait(false);

                // Read response with async I/O
                string response = await ReadResponseAsync(portSnapshot, timeoutMs, linkedCts.Token).ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"[SerialPort] Received response: {response}");
                return response;
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[SerialPort] Command cancelled");
                throw;
            }
            catch (TimeoutException)
            {
                System.Diagnostics.Debug.WriteLine($"[SerialPort] Timeout waiting for response after {timeoutMs}ms");
                throw new TimeoutException($"No response received within {timeoutMs}ms");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SerialPort] Communication error: {ex.Message}");
                throw new InvalidOperationException($"Serial communication error: {ex.Message}", ex);
            }
            finally
            {
                _asyncLock.Release();
            }
        }

        /// <summary>
        /// Synchronous wrapper for backwards compatibility.
        /// Prefer SendCommandAsync for better performance.
        /// </summary>
        public string SendCommand(string command, int timeoutMs = READ_TIMEOUT_MS)
        {
            return SendCommandAsync(command, timeoutMs, CancellationToken.None)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        /// <summary>
        /// Sends a command synchronously without cancellation token support.
        /// Used for critical shutdown commands that must complete.
        /// </summary>
        public string SendCommandSync(string command, int timeoutMs = READ_TIMEOUT_MS)
        {
            if (!_asyncLock.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Failed to acquire serial port lock");
            }

            try
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                {
                    throw new InvalidOperationException("Serial port is not connected");
                }

                // Clear any pending data
                if (_serialPort.BytesToRead > 0)
                {
                    _serialPort.DiscardInBuffer();
                }

                // Send command with CRLF terminator
                System.Diagnostics.Debug.WriteLine($"[SerialPort] SendCommandSync: {command}");
                
                byte[] commandBytes = Encoding.ASCII.GetBytes(command + "\r\n");
                _serialPort.Write(commandBytes, 0, commandBytes.Length);

                // Read response synchronously
                var responseBuilder = new StringBuilder();
                bool foundStart = false;
                int expectedLength = -1;
                var startTime = DateTime.UtcNow;

                while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
                {
                    if (_serialPort.BytesToRead > 0)
                    {
                        char c = (char)_serialPort.ReadChar();

                        if (c == '<')
                        {
                            foundStart = true;
                            responseBuilder.Clear();
                            expectedLength = -1;
                        }

                        if (foundStart)
                        {
                            responseBuilder.Append(c);

                            // Parse length after receiving header
                            if (responseBuilder.Length == 9 && expectedLength == -1)
                            {
                                try
                                {
                                    string lengthStr = responseBuilder.ToString().Substring(5, 4);
                                    int dataLength = int.Parse(lengthStr, System.Globalization.NumberStyles.HexNumber);
                                    expectedLength = 9 + (dataLength * 2);
                                }
                                catch { }
                            }

                            // Check if complete
                            if (expectedLength > 0 && responseBuilder.Length >= expectedLength)
                            {
                                string response = responseBuilder.ToString();
                                System.Diagnostics.Debug.WriteLine($"[SerialPort] SendCommandSync response: {response}");
                                return response;
                            }
                        }
                    }
                    else
                    {
                        // No data available, brief sleep
                        Thread.Sleep(10);
                    }
                }

                throw new TimeoutException($"No response received within {timeoutMs}ms");
            }
            finally
            {
                _asyncLock.Release();
            }
        }

        /// <summary>
        /// Reads a complete response asynchronously using true async I/O.
        /// Response format: <CCDDLLLL[DATA]
        /// Optimized for responsiveness under high CPU load.
        /// </summary>
        private async Task<string> ReadResponseAsync(SerialPort serialPort, int timeoutMs, CancellationToken cancellationToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            var buffer = new byte[256];
            var responseBuilder = new StringBuilder();
            bool foundStart = false;
            int expectedLength = -1;
            int emptyReadCount = 0;
            const int maxEmptyReads = 10; // Increased from 3 to be more patient

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    int bytesRead = 0;
                    
                    try
                    {
                        // TRUE ASYNC I/O - Thread is released during read operation!
                        bytesRead = await serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
                    {
                        // Timeout or cancellation
                        break;
                    }

                    if (bytesRead == 0)
                    {
                        emptyReadCount++;
                        
                        // OPTIMIZATION: Only yield if we've had multiple empty reads
                        // This prevents Task.Delay overhead under high CPU load
                        // Serial data arrives in bursts, so immediate retry is often successful
                        if (emptyReadCount >= maxEmptyReads)
                        {
                            // Yield briefly to prevent tight loop, but resume ASAP
                            // Task.Yield is much lighter than Task.Delay under load
                            await Task.Yield();
                            emptyReadCount = 0;
                        }
                        continue;
                    }

                    // Reset empty read counter when we get data
                    emptyReadCount = 0;

                    // Process received bytes
                    for (int i = 0; i < bytesRead; i++)
                    {
                        char c = (char)buffer[i];

                        // Start collecting when we see '<'
                        if (c == '<')
                        {
                            foundStart = true;
                            responseBuilder.Clear();
                            expectedLength = -1;
                        }

                        if (foundStart)
                        {
                            responseBuilder.Append(c);

                            // Check if we have enough data to parse length
                            if (responseBuilder.Length == 9 && expectedLength == -1)
                            {
                                // Try to parse the length field: <CCDDLLLL
                                try
                                {
                                    string lengthStr = responseBuilder.ToString().Substring(5, 4);
                                    int dataLength = int.Parse(lengthStr, System.Globalization.NumberStyles.HexNumber);
                                    
                                    // Calculate expected total length: 9 (header) + (dataLength * 2 for hex encoding)
                                    expectedLength = 9 + (dataLength * 2);
                                    
                                    System.Diagnostics.Debug.WriteLine($"[SerialPort] Expecting {expectedLength} total chars (header=9 + data={dataLength*2})");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[SerialPort] Failed to parse length: {ex.Message}");
                                    // If we can't parse length, continue reading
                                }
                            }

                            // Check if we have complete message
                            if (expectedLength > 0 && responseBuilder.Length >= expectedLength)
                            {
                                string response = responseBuilder.ToString();
                                System.Diagnostics.Debug.WriteLine($"[SerialPort] Complete response received: {responseBuilder.Length} chars");
                                return response;
                            }
                        }
                    }
                    
                    // Log progress for large responses
                    if (foundStart && expectedLength > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SerialPort] Progress: {responseBuilder.Length}/{expectedLength} chars");
                    }
                }

                // Timeout or cancellation
                if (!foundStart)
                {
                    System.Diagnostics.Debug.WriteLine($"[SerialPort] Timeout: No start character '<' received after {timeoutMs}ms");
                    throw new TimeoutException("No response start character '<' received");
                }

                string partialResponse = responseBuilder.ToString();
                if (partialResponse.Length > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[SerialPort] Partial response (timeout): {partialResponse}");
                    System.Diagnostics.Debug.WriteLine($"[SerialPort] Expected {expectedLength} chars, got {partialResponse.Length}");
                }

                throw new TimeoutException($"Incomplete response received: expected {expectedLength}, got {partialResponse.Length} chars");
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Timeout occurred
                if (!foundStart)
                {
                    System.Diagnostics.Debug.WriteLine($"[SerialPort] Timeout: No start character '<' received");
                    throw new TimeoutException("No response start character '<' received");
                }

                string partialResponse = responseBuilder.ToString();
                System.Diagnostics.Debug.WriteLine($"[SerialPort] Timeout: Expected {expectedLength} chars, got {partialResponse.Length}");
                throw new TimeoutException($"Incomplete response received: expected {expectedLength}, got {partialResponse.Length} chars");
            }
        }

        /// <summary>
        /// Finds the COM port for the VU1 Gauge Hub by VID/PID.
        /// Note: This is a simplified implementation. In production, use WMI or SetupAPI.
        /// </summary>
        private string? FindVU1Port()
        {
            try
            {
                // Get all available ports
                string[] ports = SerialPort.GetPortNames();
                System.Diagnostics.Debug.WriteLine($"[SerialPort] Found {ports.Length} available COM port(s): {string.Join(", ", ports)}");

                if (ports.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[SerialPort] No COM ports available");
                    return null;
                }

                // Try each port
                foreach (string port in ports)
                {
                    System.Diagnostics.Debug.WriteLine($"[SerialPort] Attempting to detect VU1 hub on: {port}");
                    
                    if (TryPort(port))
                    {
                        System.Diagnostics.Debug.WriteLine($"[SerialPort] ? Found VU1 hub on port: {port}");
                        return port;
                    }
                }

                System.Diagnostics.Debug.WriteLine("[SerialPort] No VU1 hub found on any port - trying fallback with first port");
                
                // Fallback: Try to connect to first port without validation
                if (ports.Length > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[SerialPort] Attempting fallback connection to first port: {ports[0]}");
#pragma warning disable CS8600
                    return ports[0];
#pragma warning restore CS8600
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SerialPort] Port discovery error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Tests if a port is the VU1 hub by attempting a rescan command (async version).
        /// Optimized for high CPU load scenarios.
        /// </summary>
        private async Task<bool> TryPortAsync(string portName, CancellationToken cancellationToken = default)
        {
            SerialPort? testPort = null;
            try
            {
                testPort = new SerialPort(portName)
                {
                    BaudRate = BAUD_RATE,
                    ReadTimeout = SerialPort.InfiniteTimeout,
                    WriteTimeout = SerialPort.InfiniteTimeout,
                    Handshake = Handshake.None,
                    DtrEnable = true,
                    RtsEnable = true
                };

                testPort.Open();
                
                // Wait for port to stabilize
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                
                // Clear any junk in buffer
                if (testPort.BytesToRead > 0)
                {
                    testPort.DiscardInBuffer();
                }

                System.Diagnostics.Debug.WriteLine($"[SerialPort] Testing {portName}: sending RESCAN_BUS command");
                
                // Send a simple RESCAN_BUS command
                byte[] command = Encoding.ASCII.GetBytes(">0C0100000000\r\n");
                await testPort.BaseStream.WriteAsync(command, 0, command.Length, cancellationToken).ConfigureAwait(false);
                await testPort.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                // Try to read response with timeout (async, optimized for high load)
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(2000);

                var buffer = new byte[128];
                var responseBuilder = new StringBuilder();
                bool foundStart = false;
                int emptyReadCount = 0;

                try
                {
                    while (!cts.Token.IsCancellationRequested && responseBuilder.Length < 100)
                    {
                        int bytesRead = await testPort.BaseStream.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false);
                        
                        if (bytesRead == 0)
                        {
                            emptyReadCount++;
                            
                            // Only yield after multiple empty reads to reduce overhead
                            if (emptyReadCount >= 3)
                            {
                                await Task.Yield();
                                emptyReadCount = 0;
                            }
                            continue;
                        }

                        emptyReadCount = 0;

                        for (int i = 0; i < bytesRead; i++)
                        {
                            char c = (char)buffer[i];
                            
                            if (c == '<')
                            {
                                foundStart = true;
                                responseBuilder.Clear();
                            }
                            
                            if (foundStart)
                            {
                                responseBuilder.Append(c);
                            }
                        }

                        // Check if we have enough for validation
                        if (responseBuilder.Length >= 9)
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Timeout
                }

                string response = responseBuilder.ToString();
                System.Diagnostics.Debug.WriteLine($"[SerialPort] {portName} response: '{response}'");

                // Check if we got any valid response
                bool isHub = response.Length >= 9 && response.StartsWith("<");
                
                if (isHub && response.Length >= 2)
                {
                    System.Diagnostics.Debug.WriteLine($"[SerialPort] {portName} responded with command code: {response.Substring(1, 2)}");
                }
                
                System.Diagnostics.Debug.WriteLine($"[SerialPort] {portName} is VU1 hub: {isHub}");
                return isHub;
            }
            catch (UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[SerialPort] {portName}: Access denied - port may be in use");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SerialPort] TryPortAsync({portName}) failed: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
            finally
            {
                if (testPort != null)
                {
                    try
                    {
                        if (testPort.IsOpen)
                        {
                            testPort.Close();
                        }
                        testPort.Dispose();
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Synchronous wrapper for TryPort (backwards compatibility during port detection).
        /// </summary>
        private bool TryPort(string portName)
        {
            return TryPortAsync(portName, CancellationToken.None)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Disconnect();
                
                // Only dispose the cancellation token source, NOT the semaphore
                // The semaphore may still have pending Release() calls from cancelled operations
                _disconnectCts?.Dispose();
                
                // Do NOT dispose _asyncLock here - it causes ObjectDisposedException
                // when cancelled operations try to release it in their finally blocks.
                // The GC will clean it up when the object is finalized.
            }
        }
    }
}
