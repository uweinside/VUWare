using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;

namespace VUWare.Lib
{
    /// <summary>
    /// Manages serial port communication with the VU1 Gauge Hub.
    /// Handles USB device detection, port configuration, and thread-safe communication.
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
        private readonly object _lockObj = new object();
        private bool _isConnected;
        private byte[] _responseBuffer = new byte[RESPONSE_BUFFER_SIZE];
#pragma warning disable CS0414
        private int _bufferIndex = 0;
#pragma warning restore CS0414

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
            lock (_lockObj)
            {
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
                    _isConnected = true;
                    _bufferIndex = 0;
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Connection failed: {ex.Message}");
                    _isConnected = false;
                    return false;
                }
            }
        }

        /// <summary>
        /// Disconnects from the VU1 hub.
        /// </summary>
        public void Disconnect()
        {
            lock (_lockObj)
            {
                if (_serialPort != null)
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }
                    _serialPort.Dispose();
                    _serialPort = null;
                }
                _isConnected = false;
            }
        }

        /// <summary>
        /// Sends a command to the hub and waits for a response.
        /// Thread-safe; only one command can be in-flight at a time.
        /// </summary>
        public string SendCommand(string command, int timeoutMs = READ_TIMEOUT_MS)
        {
            lock (_lockObj)
            {
                if (!_isConnected || _serialPort == null || !_serialPort.IsOpen)
                {
                    throw new InvalidOperationException("Serial port is not connected");
                }

                try
                {
                    // Clear any pending data
                    if (_serialPort.BytesToRead > 0)
                    {
                        _serialPort.DiscardInBuffer();
                    }

                    // Send command with CRLF terminator
                    System.Diagnostics.Debug.WriteLine($"[SerialPort] Sending command: {command}");
                    _serialPort.WriteLine(command);
                    _serialPort.BaseStream.Flush();

                    // Read response with improved timeout handling
                    string response = ReadResponseWithTimeout(timeoutMs);
                    System.Diagnostics.Debug.WriteLine($"[SerialPort] Received response: {response}");
                    return response;
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
            }
        }

        /// <summary>
        /// Reads a complete response with improved timeout and buffer management.
        /// Response format: <CCDDLLLL[DATA]
        /// </summary>
        private string ReadResponseWithTimeout(int timeoutMs)
        {
            Stopwatch timeout = Stopwatch.StartNew();
            string response = string.Empty;
            bool foundStart = false;

            while (timeout.ElapsedMilliseconds < timeoutMs)
            {
                if (_serialPort?.BytesToRead > 0)
                {
                    try
                    {
                        char c = (char)_serialPort.ReadByte();
                        
                        // Start collecting when we see '<'
                        if (c == '<')
                        {
                            foundStart = true;
                            response = string.Empty;
                        }

                        if (foundStart)
                        {
                            response += c;

                            // Check if we have a complete message
                            // Minimum: <CCDDLLLL (9 characters)
                            if (response.Length >= 9)
                            {
                                // Try to parse the length field to know how much more data we need
                                try
                                {
                                    string lengthStr = response.Substring(5, 4);
                                    int dataLength = int.Parse(lengthStr, System.Globalization.NumberStyles.HexNumber);
                                    
                                    // Calculate expected total length: 9 (header) + (dataLength * 2 for hex encoding)
                                    int expectedLength = 9 + (dataLength * 2);
                                    
                                    if (response.Length >= expectedLength)
                                    {
                                        // We have the complete message
                                        return response;
                                    }
                                }
                                catch
                                {
                                    // If we can't parse length, wait for more data
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SerialPort] Read error: {ex.Message}");
                    }
                }
                else
                {
                    Thread.Sleep(1);
                }
            }

            if (!foundStart)
            {
                throw new TimeoutException("No response start character '<' received");
            }

            if (response.Length > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[SerialPort] Partial response (timeout): {response}");
            }

            throw new TimeoutException($"Incomplete response received: {response}");
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
        /// Tests if a port is the VU1 hub by attempting a rescan command.
        /// </summary>
        private bool TryPort(string portName)
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
                System.Threading.Thread.Sleep(100);
                
                // Clear any junk in buffer
                if (testPort.BytesToRead > 0)
                {
                    testPort.DiscardInBuffer();
                }

                System.Diagnostics.Debug.WriteLine($"[SerialPort] Testing {portName}: sending RESCAN_BUS command");
                
                // Send a simple RESCAN_BUS command
                testPort.WriteLine(">0C0100000000");
                testPort.BaseStream.Flush();

                // Try to read response with longer timeout
                Stopwatch sw = Stopwatch.StartNew();
                string response = string.Empty;
                bool foundStart = false;

                // Wait up to 2 seconds for response
                while (sw.ElapsedMilliseconds < 2000 && response.Length < 100)
                {
                    if (testPort.BytesToRead > 0)
                    {
                        try
                        {
                            char c = (char)testPort.ReadByte();
                            
                            if (c == '<')
                            {
                                foundStart = true;
                                response = string.Empty;
                            }
                            
                            if (foundStart)
                            {
                                response += c;
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[SerialPort] Error reading from {portName}: {ex.Message}");
                            break;
                        }
                    }
                    else
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[SerialPort] {portName} response: '{response}'");

                // Check if we got any valid response
                // Accept any response that starts with '<' and contains at least the header
                bool isHub = response.Length >= 9 && response.StartsWith("<");
                
                if (isHub && response.Length >= 2)
                {
                    // Log the command code for debugging
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
                System.Diagnostics.Debug.WriteLine($"[SerialPort] TryPort({portName}) failed: {ex.GetType().Name}: {ex.Message}");
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

        public void Dispose()
        {
            Disconnect();
        }
    }
}
