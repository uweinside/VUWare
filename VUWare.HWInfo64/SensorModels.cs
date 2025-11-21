using System;

namespace VUWare.HWInfo64
{
    /// <summary>
    /// Represents a single sensor reading from HWInfo64.
    /// Provides a friendly API for consuming sensor data.
    /// </summary>
    public class SensorReading
    {
        /// <summary>Unique identifier of the sensor</summary>
        public uint SensorId { get; set; }

        /// <summary>Instance number of the sensor (for multiple sensors of same type)</summary>
        public uint SensorInstance { get; set; }

        /// <summary>Name of the sensor (user name if set, otherwise original name)</summary>
        public string SensorName { get; set; } = string.Empty;

        /// <summary>Unique identifier of this entry</summary>
        public uint EntryId { get; set; }

        /// <summary>Name of this entry (user name if set, otherwise original name)</summary>
        public string EntryName { get; set; } = string.Empty;

        /// <summary>Type of sensor (Temperature, Voltage, Fan, etc.)</summary>
        public SensorType Type { get; set; }

        /// <summary>Current value of the sensor</summary>
        public double Value { get; set; }

        /// <summary>Minimum recorded value</summary>
        public double ValueMin { get; set; }

        /// <summary>Maximum recorded value</summary>
        public double ValueMax { get; set; }

        /// <summary>Average value (if tracked by HWInfo64)</summary>
        public double ValueAvg { get; set; }

        /// <summary>Unit of measurement (e.g., "°C", "V", "RPM", "%")</summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>Timestamp of last update</summary>
        public DateTime LastUpdate { get; set; }

        /// <summary>
        /// Gets a display-friendly string representation of the reading.
        /// </summary>
        /// <returns>Formatted string like "CPU Temperature: 45.3 °C"</returns>
        public override string ToString()
        {
            return $"{SensorName} - {EntryName}: {Value:0.##} {Unit}";
        }

        /// <summary>
        /// Gets a detailed string representation including min/max/avg.
        /// </summary>
        public string ToDetailedString()
        {
            return $"{SensorName} - {EntryName}: {Value:0.##} {Unit} (min: {ValueMin:0.##}, max: {ValueMax:0.##}, avg: {ValueAvg:0.##})";
        }
    }

    /// <summary>
    /// Represents a collection of sensor readings grouped by sensor.
    /// </summary>
    public class SensorSnapshot
    {
        /// <summary>Timestamp when this snapshot was taken</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>All sensor readings in this snapshot</summary>
        public List<SensorReading> Readings { get; set; } = new();

        /// <summary>
        /// Gets all readings for a specific sensor by name.
        /// </summary>
        public List<SensorReading> GetSensorReadings(string sensorName)
        {
            return Readings.Where(r => r.SensorName.Equals(sensorName, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>
        /// Gets all readings of a specific type.
        /// </summary>
        public List<SensorReading> GetReadingsByType(SensorType type)
        {
            return Readings.Where(r => r.Type == type).ToList();
        }

        /// <summary>
        /// Gets the first reading matching the given criteria.
        /// </summary>
        public SensorReading? FindReading(string sensorName, string entryName)
        {
            return Readings.FirstOrDefault(r =>
                r.SensorName.Equals(sensorName, StringComparison.OrdinalIgnoreCase) &&
                r.EntryName.Equals(entryName, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Represents a sensor that can be displayed on a VU1 dial.
    /// Maps a HWInfo64 sensor reading to a dial display range.
    /// </summary>
    public class DialSensorMapping
    {
        /// <summary>Unique identifier for this mapping</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Name of the sensor to display (from HWInfo64)</summary>
        public string SensorName { get; set; } = string.Empty;

        /// <summary>Name of the specific entry to display (from HWInfo64)</summary>
        public string EntryName { get; set; } = string.Empty;

        /// <summary>Minimum value for dial display (0%)</summary>
        public double MinValue { get; set; }

        /// <summary>Maximum value for dial display (100%)</summary>
        public double MaxValue { get; set; }

        /// <summary>Optional warning threshold (shows color change on dial)</summary>
        public double? WarningThreshold { get; set; }

        /// <summary>Optional critical threshold (shows color change on dial)</summary>
        public double? CriticalThreshold { get; set; }

        /// <summary>Display name for the dial</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Gets the percentage (0-100) for the current sensor value.</summary>
        public byte GetPercentage(double sensorValue)
        {
            if (MaxValue <= MinValue)
                return 0;

            double percentage = (sensorValue - MinValue) / (MaxValue - MinValue) * 100.0;
            return (byte)Math.Clamp(percentage, 0, 100);
        }

        /// <summary>Determines if the value exceeds the critical threshold.</summary>
        public bool IsCritical(double sensorValue)
        {
            return CriticalThreshold.HasValue && sensorValue >= CriticalThreshold.Value;
        }

        /// <summary>Determines if the value exceeds the warning threshold.</summary>
        public bool IsWarning(double sensorValue)
        {
            return WarningThreshold.HasValue && sensorValue >= WarningThreshold.Value && !IsCritical(sensorValue);
        }
    }
}
