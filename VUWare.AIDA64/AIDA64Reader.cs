// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace VUWare.AIDA64
{
    /// <summary>
    /// Reads AIDA64 sensor data from shared memory.
    /// AIDA64 exposes data as XML in a memory-mapped file named "AIDA64_SensorValues".
    /// </summary>
    /// <remarks>
    /// To enable shared memory in AIDA64:
    /// 1. Open AIDA64 ? File ? Preferences
    /// 2. Navigate to External Applications ? Shared Memory
    /// 3. Check "Enable Shared Memory"
    /// 4. Optionally adjust the update interval (default 1000ms)
    /// </remarks>
    public class AIDA64Reader : IDisposable
    {
        private const string AIDA64_MAP_NAME = "AIDA64_SensorValues";
        private const int MAX_BUFFER_SIZE = 65536; // 64KB should be plenty for sensor XML

        private MemoryMappedFile? _mmf;
        private MemoryMappedViewAccessor? _accessor;
        private bool _disposed;
        private bool _isConnected;
        private DateTime _lastReadTime = DateTime.MinValue;
        private int _consecutiveTimeouts;

        /// <summary>
        /// Gets whether the reader is currently connected to AIDA64 shared memory.
        /// </summary>
        public bool IsConnected => _isConnected;

        /// <summary>
        /// Attempts to connect to AIDA64 shared memory.
        /// AIDA64 must be running with "External Applications > Enable Shared Memory" enabled.
        /// </summary>
        /// <returns>True if connection was successful, false otherwise.</returns>
        public bool Connect()
        {
            try
            {
                _mmf = MemoryMappedFile.OpenExisting(AIDA64_MAP_NAME);
                _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
                _isConnected = true;
                _consecutiveTimeouts = 0;
                System.Diagnostics.Debug.WriteLine("[AIDA64Reader] Connected to shared memory");
                return true;
            }
            catch (FileNotFoundException)
            {
                System.Diagnostics.Debug.WriteLine("[AIDA64Reader] Shared memory not found - AIDA64 not running or sharing disabled");
                _isConnected = false;
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AIDA64Reader] Connection failed: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Disconnects from AIDA64 shared memory.
        /// </summary>
        public void Disconnect()
        {
            _accessor?.Dispose();
            _mmf?.Dispose();
            _accessor = null;
            _mmf = null;
            _isConnected = false;
            System.Diagnostics.Debug.WriteLine("[AIDA64Reader] Disconnected");
        }

        /// <summary>
        /// Reads the raw XML string from shared memory.
        /// </summary>
        /// <returns>The raw XML content, or null if read fails.</returns>
        public string? ReadRawXml()
        {
            if (!_isConnected || _accessor == null)
                return null;

            try
            {
                byte[] buffer = new byte[MAX_BUFFER_SIZE];
                _accessor.ReadArray(0, buffer, 0, buffer.Length);

                // Find null terminator or end of data
                int length = Array.IndexOf(buffer, (byte)0);
                if (length <= 0)
                    length = buffer.Length;

                string xml = Encoding.ASCII.GetString(buffer, 0, length);

                // AIDA64 doesn't wrap in a root element, so we need to add one for valid XML parsing
                if (!string.IsNullOrWhiteSpace(xml))
                {
                    xml = $"<aida64>{xml}</aida64>";
                }

                return xml;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AIDA64Reader] Error reading XML: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parses shared memory and returns all sensor readings.
        /// Now with timeout protection against blocking reads under high CPU load.
        /// </summary>
        /// <returns>List of sensor readings, or empty list if read fails.</returns>
        public List<AIDA64RawReading> ReadAllSensorReadings()
        {
            var readings = new List<AIDA64RawReading>();

            if (!_isConnected || _accessor == null)
                return readings;

            try
            {
                var readStartTime = DateTime.Now;
                var timeSinceLastRead = _lastReadTime == DateTime.MinValue
                    ? 0
                    : (readStartTime - _lastReadTime).TotalMilliseconds;

                // Wrap read operation in a timeout to prevent blocking under high load
                var readTask = Task.Run(() => ParseSensorData());

                // Wait for read with 100ms timeout
                if (readTask.Wait(100))
                {
                    _lastReadTime = DateTime.Now;
                    var totalElapsed = (_lastReadTime - readStartTime).TotalMilliseconds;
                    _consecutiveTimeouts = 0;

                    if (timeSinceLastRead > 0)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[AIDA64Reader] ? SUCCESS - Gap since last: {timeSinceLastRead:F0}ms, " +
                            $"This read took: {totalElapsed:F0}ms, Got {readTask.Result.Count} readings");
                    }

                    return readTask.Result;
                }
                else
                {
                    _consecutiveTimeouts++;
                    var totalElapsed = (DateTime.Now - readStartTime).TotalMilliseconds;
                    System.Diagnostics.Debug.WriteLine(
                        $"[AIDA64Reader] ? TIMEOUT #{_consecutiveTimeouts} - Read blocked for {totalElapsed:F0}ms " +
                        $"(last success: {timeSinceLastRead:F0}ms ago)");
                    return readings;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AIDA64Reader] Outer exception: {ex.Message}");
                return readings;
            }
        }

        /// <summary>
        /// Internal method to parse sensor data from shared memory XML.
        /// </summary>
        private List<AIDA64RawReading> ParseSensorData()
        {
            var readings = new List<AIDA64RawReading>();

            string? xml = ReadRawXml();
            if (string.IsNullOrWhiteSpace(xml))
                return readings;

            try
            {
                var doc = XDocument.Parse(xml);
                var root = doc.Root;
                if (root == null)
                    return readings;

                // AIDA64 uses element names to indicate sensor type:
                // sys = system, temp = temperature, fan = fan speed, volt = voltage, etc.
                foreach (var element in root.Elements())
                {
                    string elementName = element.Name.LocalName.ToLowerInvariant();
                    var category = MapElementToCategory(elementName);

                    string? id = element.Element("id")?.Value;
                    string? label = element.Element("label")?.Value;
                    string? valueStr = element.Element("value")?.Value;

                    if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(label))
                        continue;

                    // Parse value (handle different formats, remove non-numeric chars)
                    double value = 0;
                    if (!string.IsNullOrEmpty(valueStr))
                    {
                        // Remove any non-numeric characters except decimal point and minus
                        string cleanValue = Regex.Replace(valueStr, @"[^\d.\-]", "");
                        double.TryParse(cleanValue, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out value);
                    }

                    readings.Add(new AIDA64RawReading
                    {
                        Id = id,
                        Label = label,
                        Value = value,
                        RawValue = valueStr ?? string.Empty,
                        Category = category,
                        Unit = GetUnitForCategory(category),
                        RawElementType = elementName,
                        LastUpdate = DateTime.Now
                    });
                }

                System.Diagnostics.Debug.WriteLine($"[AIDA64Reader] Parsed {readings.Count} sensor readings");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AIDA64Reader] Error parsing XML: {ex.Message}");
            }

            return readings;
        }

        /// <summary>
        /// Maps AIDA64 XML element names to sensor categories.
        /// </summary>
        /// <remarks>
        /// AIDA64 element types:
        /// - sys: System values (clocks, utilization)
        /// - temp: Temperatures
        /// - volt: Voltages
        /// - fan: Fan speeds (RPM)
        /// - duty: Fan duty cycles (%)
        /// - pwr: Power consumption (W)
        /// - curr: Current (A)
        /// </remarks>
        private static AIDA64SensorCategory MapElementToCategory(string elementName) => elementName switch
        {
            "temp" => AIDA64SensorCategory.Temperature,
            "volt" => AIDA64SensorCategory.Voltage,
            "fan" => AIDA64SensorCategory.Fan,
            "duty" => AIDA64SensorCategory.FanDuty,
            "pwr" => AIDA64SensorCategory.Power,
            "curr" => AIDA64SensorCategory.Current,
            "sys" => AIDA64SensorCategory.System,
            _ => AIDA64SensorCategory.Other
        };

        /// <summary>
        /// Gets the typical unit of measurement for a sensor category.
        /// </summary>
        private static string GetUnitForCategory(AIDA64SensorCategory category) => category switch
        {
            AIDA64SensorCategory.Temperature => "°C",
            AIDA64SensorCategory.Voltage => "V",
            AIDA64SensorCategory.Fan => "RPM",
            AIDA64SensorCategory.FanDuty => "%",
            AIDA64SensorCategory.Power => "W",
            AIDA64SensorCategory.Current => "A",
            AIDA64SensorCategory.System => "",
            _ => ""
        };

        public void Dispose()
        {
            if (!_disposed)
            {
                Disconnect();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Categories for AIDA64 sensor readings based on XML element types.
    /// </summary>
    public enum AIDA64SensorCategory
    {
        /// <summary>System values (clocks, utilization, misc)</summary>
        System,
        /// <summary>Temperature sensors</summary>
        Temperature,
        /// <summary>Voltage sensors</summary>
        Voltage,
        /// <summary>Fan speed sensors (RPM)</summary>
        Fan,
        /// <summary>Fan duty cycle sensors (%)</summary>
        FanDuty,
        /// <summary>Power consumption sensors (W)</summary>
        Power,
        /// <summary>Current sensors (A)</summary>
        Current,
        /// <summary>Other/unknown sensor types</summary>
        Other
    }

    /// <summary>
    /// Represents a raw sensor reading directly from AIDA64 shared memory.
    /// This is the internal representation before adaptation to ISensorReading.
    /// </summary>
    public class AIDA64RawReading
    {
        /// <summary>Unique sensor ID from AIDA64 (e.g., "TCPU", "SCPUCLK")</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Human-readable label (e.g., "CPU Package", "CPU Clock")</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>Parsed numeric sensor value</summary>
        public double Value { get; set; }

        /// <summary>Original raw value string from AIDA64</summary>
        public string RawValue { get; set; } = string.Empty;

        /// <summary>Sensor category determined from XML element type</summary>
        public AIDA64SensorCategory Category { get; set; }

        /// <summary>Unit of measurement</summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>Raw XML element type (for debugging)</summary>
        public string RawElementType { get; set; } = string.Empty;

        /// <summary>When this reading was captured</summary>
        public DateTime LastUpdate { get; set; }
    }
}
