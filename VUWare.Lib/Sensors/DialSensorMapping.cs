// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace VUWare.Lib.Sensors
{
    /// <summary>
    /// Represents a mapping between a VU1 dial and a sensor reading.
    /// This is a provider-agnostic configuration that can work with any ISensorProvider.
    /// </summary>
    public class DialSensorMapping
    {
        /// <summary>
        /// Unique identifier for this mapping (typically the dial UID).
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Name of the sensor to monitor (e.g., "CPU Package", "GPU Core").
        /// </summary>
        public string SensorName { get; set; } = string.Empty;

        /// <summary>
        /// Sensor ID for precise matching (provider-specific, 0 = match by name only).
        /// </summary>
        public uint SensorId { get; set; } = 0;

        /// <summary>
        /// Sensor instance for precise matching (provider-specific, 0 = match by name only).
        /// </summary>
        public uint SensorInstance { get; set; } = 0;

        /// <summary>
        /// Name of the specific entry/reading to monitor (e.g., "Temperature", "Load").
        /// </summary>
        public string EntryName { get; set; } = string.Empty;

        /// <summary>
        /// Entry ID for precise matching (provider-specific, 0 = match by name only).
        /// </summary>
        public uint EntryId { get; set; } = 0;

        /// <summary>
        /// Minimum value for dial display (maps to 0%).
        /// </summary>
        public double MinValue { get; set; }

        /// <summary>
        /// Maximum value for dial display (maps to 100%).
        /// </summary>
        public double MaxValue { get; set; }

        /// <summary>
        /// Optional warning threshold value.
        /// </summary>
        public double? WarningThreshold { get; set; }

        /// <summary>
        /// Optional critical threshold value.
        /// </summary>
        public double? CriticalThreshold { get; set; }

        /// <summary>
        /// Display name for the dial.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Calculates the percentage (0-100) for a given sensor value.
        /// </summary>
        public byte GetPercentage(double sensorValue)
        {
            if (MaxValue <= MinValue)
                return 0;

            double percentage = (sensorValue - MinValue) / (MaxValue - MinValue) * 100.0;
            return (byte)Math.Clamp(percentage, 0, 100);
        }

        /// <summary>
        /// Determines if the value exceeds the critical threshold.
        /// </summary>
        public bool IsCritical(double sensorValue)
        {
            return CriticalThreshold.HasValue && sensorValue >= CriticalThreshold.Value;
        }

        /// <summary>
        /// Determines if the value exceeds the warning threshold (but not critical).
        /// </summary>
        public bool IsWarning(double sensorValue)
        {
            return WarningThreshold.HasValue && 
                   sensorValue >= WarningThreshold.Value && 
                   !IsCritical(sensorValue);
        }
    }

    /// <summary>
    /// Represents the current status of a mapped sensor for dial display.
    /// </summary>
    public class DialSensorStatus
    {
        /// <summary>
        /// The mapping ID (dial UID).
        /// </summary>
        public string MappingId { get; set; } = string.Empty;

        /// <summary>
        /// The current sensor reading (if available).
        /// </summary>
        public ISensorReading? SensorReading { get; set; }

        /// <summary>
        /// The calculated percentage for the dial (0-100).
        /// </summary>
        public byte Percentage { get; set; }

        /// <summary>
        /// Whether the value is in critical range.
        /// </summary>
        public bool IsCritical { get; set; }

        /// <summary>
        /// Whether the value is in warning range.
        /// </summary>
        public bool IsWarning { get; set; }

        /// <summary>
        /// Gets recommended backlight color based on status.
        /// </summary>
        public (byte Red, byte Green, byte Blue) GetRecommendedColor()
        {
            if (IsCritical)
                return (100, 0, 0);   // Red
            if (IsWarning)
                return (100, 50, 0);  // Orange
            return (0, 100, 0);       // Green (normal)
        }
    }
}
