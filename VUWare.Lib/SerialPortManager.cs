using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

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

        private SerialPort? _serialPort;
        private readonly object _lockObj = new object();
        private bool _isConnected;

        public bool IsConnected => _isConnected;

        /// <summary>
        /// Attempts to connect to the VU1 Gauge Hub by auto-detecting the USB port.
        /// </summary>
        public bool AutoDetectAndConnect()
        {
            try
            {
                string portName = FindVU1Port();
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
                        WriteTimeout = WRITE_TIMEOUT_MS
                    };

                    _serialPort.Open();
                    _isConnected = true;
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
                    // Send command with CRLF terminator
                    _serialPort.WriteLine(command);
                    _serialPort.BaseStream.Flush();

                    // Read response
                    string response = ReadResponse(timeoutMs);
                    return response;
                }
                catch (TimeoutException)
                {
                    throw new TimeoutException($"No response received within {timeoutMs}ms");
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Serial communication error: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Reads a complete response line from the serial port.
        /// Response format: <CCDDLLLL[DATA]<CR><LF>
        /// </summary>
        private string ReadResponse(int timeoutMs)
        {
            DateTime startTime = DateTime.UtcNow;
            string response = string.Empty;

            while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
            {
                if (_serialPort.BytesToRead > 0)
                {
                    char c = (char)_serialPort.ReadByte();
                    
                    // Wait for start character
                    if (c == '<' || response.Length > 0)
                    {
                        response += c;

                        // Check for line terminator
                        if (response.EndsWith("\r\n"))
                        {
                            // Remove line terminator and return
                            return response.Substring(0, response.Length - 2);
                        }
                    }
                }
                else
                {
                    Thread.Sleep(10);
                }
            }

            throw new TimeoutException();
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

                // This simplified version just returns the first port
                // A robust implementation would check device manager for VID/PID
                // or use Windows Management Instrumentation (WMI)
                foreach (string port in ports)
                {
                    // Try to connect and send a simple command
                    if (TryPort(port))
                    {
                        return port;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tests if a port is the VU1 hub by attempting a rescan command.
        /// </summary>
        private bool TryPort(string portName)
        {
            try
            {
                using (SerialPort testPort = new SerialPort(portName)
                {
                    BaudRate = BAUD_RATE,
                    ReadTimeout = 500,
                    WriteTimeout = 500
                })
                {
                    testPort.Open();
                    
                    // Send a simple RESCAN_BUS command
                    testPort.WriteLine(">0C0100000000");
                    testPort.BaseStream.Flush();

                    // Try to read response
                    string response = testPort.ReadLine();
                    testPort.Close();

                    // If we get a valid response starting with '<0C', it's likely the hub
                    return response != null && response.StartsWith("<0C");
                }
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
