// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using VUWare.Lib.Sensors;

namespace VUWare.AIDA64
{
    /// <summary>
    /// Represents a sensor reading from AIDA64, implementing the common ISensorReading interface.
    /// Adapts AIDA64's XML-based sensor data to the provider-agnostic abstraction.
    /// </summary>
    /// <remarks>
    /// AIDA64 has a flat structure where each reading is essentially its own sensor.
    /// To work with the Settings UI which expects a sensor-entry hierarchy:
    /// - SensorName = Category (e.g., "Temperature", "Voltage") - groups related readings
    /// - EntryName = Label (e.g., "CPU Package", "GPU Diode") - the actual sensor reading
    /// </remarks>
    public class AIDA64SensorReading : ISensorReading
    {
        /// <summary>
        /// Gets the unique AIDA64 sensor ID (e.g., "TCPU", "SCPUCLK", "TGPU1").
        /// This is the internal identifier used by AIDA64.
        /// </summary>
        public string AIDA64Id { get; init; } = string.Empty;

        /// <inheritdoc />
        /// <remarks>
        /// AIDA64 uses string IDs, so this returns a hash of the ID for compatibility.
        /// Use <see cref="AIDA64Id"/> for the original string identifier.
        /// </remarks>
        public uint SensorId { get; init; }

        /// <inheritdoc />
        public uint SensorInstance { get; init; }

        /// <inheritdoc />
        /// <remarks>
        /// For AIDA64, this represents the category grouping (e.g., "Temperature", "Voltage", "Fan").
        /// This allows the Settings UI to show sensors grouped by type.
        /// </remarks>
        public string SensorName { get; init; } = string.Empty;

        /// <inheritdoc />
        public uint EntryId { get; init; }

        /// <inheritdoc />
        /// <remarks>
        /// The human-readable label from AIDA64 (e.g., "CPU Package", "GPU Diode").
        /// This is the actual sensor name that users see and select.
        /// </remarks>
        public string EntryName { get; init; } = string.Empty;

        /// <inheritdoc />
        public SensorCategory Category { get; init; }

        /// <inheritdoc />
        public double Value { get; init; }

        /// <inheritdoc />
        /// <remarks>
        /// AIDA64 doesn't track min/max/avg values in shared memory, so these return the current value.
        /// </remarks>
        public double ValueMin { get; init; }

        /// <inheritdoc />
        public double ValueMax { get; init; }

        /// <inheritdoc />
        public double ValueAvg { get; init; }

        /// <inheritdoc />
        public string Unit { get; init; } = string.Empty;

        /// <inheritdoc />
        public DateTime LastUpdate { get; init; }

        /// <summary>
        /// Gets the original AIDA64 category before mapping to the common SensorCategory.
        /// </summary>
        public AIDA64SensorCategory OriginalCategory { get; init; }

        /// <summary>
        /// Creates a new AIDA64SensorReading from a raw reading.
        /// </summary>
        /// <param name="raw">The raw reading from AIDA64Reader.</param>
        /// <returns>A new AIDA64SensorReading instance.</returns>
        public static AIDA64SensorReading FromRaw(AIDA64RawReading raw)
        {
            var category = MapToSensorCategory(raw.Category);
            var sensorName = GetCategoryDisplayName(raw.Category);
            
            // Use a consistent SensorId for all readings in the same category
            // This ensures they get grouped together in the Settings UI
            uint sensorId = (uint)raw.Category;  // Use enum value directly for consistent grouping
            
            return new AIDA64SensorReading
            {
                AIDA64Id = raw.Id,
                SensorId = sensorId,  // Use category enum value for consistent grouping
                SensorInstance = 0,
                SensorName = sensorName,  // Use friendly category name as sensor name
                EntryId = (uint)raw.Id.GetHashCode(),  // Use AIDA64 ID hash as entry ID
                EntryName = raw.Label,  // Use the label as entry name (e.g., "CPU Package")
                Category = category,
                Value = raw.Value,
                ValueMin = raw.Value,
                ValueMax = raw.Value,
                ValueAvg = raw.Value,
                Unit = raw.Unit,
                LastUpdate = raw.LastUpdate,
                OriginalCategory = raw.Category
            };
        }

        /// <summary>
        /// Gets a user-friendly display name for an AIDA64 category.
        /// </summary>
        private static string GetCategoryDisplayName(AIDA64SensorCategory category) => category switch
        {
            AIDA64SensorCategory.Temperature => "Temperatures",
            AIDA64SensorCategory.Voltage => "Voltages",
            AIDA64SensorCategory.Fan => "Fan Speeds",
            AIDA64SensorCategory.FanDuty => "Fan Duties",
            AIDA64SensorCategory.Power => "Power",
            AIDA64SensorCategory.Current => "Currents",
            AIDA64SensorCategory.System => "System",
            _ => "Other"
        };

        /// <summary>
        /// Maps AIDA64 categories to the common SensorCategory enumeration.
        /// </summary>
        private static SensorCategory MapToSensorCategory(AIDA64SensorCategory category) => category switch
        {
            AIDA64SensorCategory.Temperature => SensorCategory.Temperature,
            AIDA64SensorCategory.Voltage => SensorCategory.Voltage,
            AIDA64SensorCategory.Fan => SensorCategory.Fan,
            AIDA64SensorCategory.FanDuty => SensorCategory.Control,
            AIDA64SensorCategory.Power => SensorCategory.Power,
            AIDA64SensorCategory.Current => SensorCategory.Current,
            AIDA64SensorCategory.System => SensorCategory.Load, // System often contains utilization %
            _ => SensorCategory.Unknown
        };
    }

    /// <summary>
    /// Represents an AIDA64 sensor descriptor for the ISensorDescriptor interface.
    /// For AIDA64, sensors are grouped by category (Temperature, Voltage, etc.).
    /// </summary>
    internal class AIDA64SensorDescriptor : ISensorDescriptor
    {
        /// <inheritdoc />
        public uint Id { get; init; }

        /// <inheritdoc />
        public uint Instance { get; init; }

        /// <inheritdoc />
        public string Name { get; init; } = string.Empty;

        /// <inheritdoc />
        public string OriginalName { get; init; } = string.Empty;
    }

    /// <summary>
    /// Reference information for common AIDA64 sensor IDs.
    /// Use these constants when configuring dial mappings.
    /// </summary>
    public static class AIDA64SensorIds
    {
        // Temperature sensors
        /// <summary>CPU Package temperature</summary>
        public const string CpuTemperature = "TCPU";
        /// <summary>GPU Diode temperature (primary GPU)</summary>
        public const string GpuTemperature = "TGPU1";
        /// <summary>GPU Diode temperature (secondary GPU)</summary>
        public const string Gpu2Temperature = "TGPU2";
        /// <summary>Motherboard temperature</summary>
        public const string MotherboardTemperature = "TMOBO";
        /// <summary>CPU core temperature</summary>
        public const string CpuCoreTemperature = "TCPUDIO";
        /// <summary>SSD/HDD temperature</summary>
        public const string StorageTemperature = "THDD1";

        // System sensors
        /// <summary>CPU Clock speed</summary>
        public const string CpuClock = "SCPUCLK";
        /// <summary>CPU Utilization</summary>
        public const string CpuUtilization = "SCPUUTI";
        /// <summary>GPU Utilization (primary)</summary>
        public const string GpuUtilization = "SGPU1UTI";
        /// <summary>Memory Utilization</summary>
        public const string MemoryUtilization = "SMEMUTI";
        /// <summary>GPU Clock</summary>
        public const string GpuClock = "SGPU1CLK";
        /// <summary>GPU Memory Clock</summary>
        public const string GpuMemoryClock = "SGPU1MEMCLK";

        // Voltage sensors
        /// <summary>CPU Core Voltage</summary>
        public const string CpuVoltage = "VCPU";
        /// <summary>GPU Core Voltage</summary>
        public const string GpuVoltage = "VGPU1";
        /// <summary>Memory Voltage</summary>
        public const string MemoryVoltage = "VDIMM";

        // Power sensors
        /// <summary>CPU Package Power</summary>
        public const string CpuPower = "PCPU";
        /// <summary>GPU Power</summary>
        public const string GpuPower = "PGPU1";

        // Fan sensors
        /// <summary>CPU Fan Speed</summary>
        public const string CpuFan = "FCPU";
        /// <summary>GPU Fan Speed</summary>
        public const string GpuFan = "FGPU1";
        /// <summary>Chassis Fan 1</summary>
        public const string ChassisFan1 = "FCHS1";
    }
}
