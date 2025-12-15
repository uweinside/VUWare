// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace VUWare.Lib.Sensors
{
    /// <summary>
    /// Represents a single sensor reading/measurement from a sensor provider.
    /// This is a point-in-time value from a specific sensor entry.
    /// </summary>
    public interface ISensorReading
    {
        /// <summary>
        /// Gets the unique identifier of the parent sensor.
        /// </summary>
        uint SensorId { get; }

        /// <summary>
        /// Gets the instance number of the parent sensor.
        /// </summary>
        uint SensorInstance { get; }

        /// <summary>
        /// Gets the display name of the parent sensor (e.g., "CPU Package", "GPU Core").
        /// </summary>
        string SensorName { get; }

        /// <summary>
        /// Gets the unique identifier of this specific entry/reading.
        /// </summary>
        uint EntryId { get; }

        /// <summary>
        /// Gets the display name of this entry (e.g., "Temperature", "Load", "Clock").
        /// </summary>
        string EntryName { get; }

        /// <summary>
        /// Gets the category/type of this sensor reading.
        /// </summary>
        SensorCategory Category { get; }

        /// <summary>
        /// Gets the current value of this reading.
        /// </summary>
        double Value { get; }

        /// <summary>
        /// Gets the minimum recorded value (if tracked by the provider).
        /// </summary>
        double ValueMin { get; }

        /// <summary>
        /// Gets the maximum recorded value (if tracked by the provider).
        /// </summary>
        double ValueMax { get; }

        /// <summary>
        /// Gets the average value (if tracked by the provider).
        /// </summary>
        double ValueAvg { get; }

        /// <summary>
        /// Gets the unit of measurement (e.g., "°C", "V", "RPM", "%", "MHz").
        /// </summary>
        string Unit { get; }

        /// <summary>
        /// Gets the timestamp of when this reading was last updated.
        /// </summary>
        DateTime LastUpdate { get; }
    }
}
