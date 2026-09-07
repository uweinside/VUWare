using System;
using VUWare.Lib.Sensors;

namespace VUWare.HWInfo64
{
    /// <summary>
    /// Represents a single sensor reading from HWInfo64.
    /// Provides a friendly API for consuming sensor data.
    /// Implements ISensorReading for use with the sensor provider abstraction.
    /// </summary>
    public class SensorReading : ISensorReading
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

        /// <summary>Type of sensor (Temperature, Voltage, Fan, etc.) - HWInfo64-specific type</summary>
        public SensorType Type { get; set; }

        /// <summary>Category of sensor - provider-agnostic type for ISensorReading interface</summary>
        public SensorCategory Category => MapSensorTypeToCategory(Type);

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
        /// Maps HWInfo64-specific SensorType to provider-agnostic SensorCategory.
        /// </summary>
        private static SensorCategory MapSensorTypeToCategory(SensorType type)
        {
            return type switch
            {
                SensorType.Temperature => SensorCategory.Temperature,
                SensorType.Voltage => SensorCategory.Voltage,
                SensorType.Fan => SensorCategory.Fan,
                SensorType.Current => SensorCategory.Current,
                SensorType.Power => SensorCategory.Power,
                SensorType.Clock => SensorCategory.Clock,
                SensorType.Usage => SensorCategory.Load,
                SensorType.Other => SensorCategory.Unknown,
                SensorType.None => SensorCategory.Unknown,
                _ => SensorCategory.Unknown
            };
        }

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

    // NOTE: DialSensorMapping has been moved to VUWare.Lib.Sensors.DialSensorMapping
    // This type alias is kept for backward compatibility
    // [Obsolete("Use VUWare.Lib.Sensors.DialSensorMapping instead")]
    // If needed, consumers should use: using DialSensorMapping = VUWare.Lib.Sensors.DialSensorMapping;
}
