using System.Runtime.InteropServices;

namespace VUWare.HWInfo64
{
    /// <summary>
    /// Native structures for reading HWInfo64 shared memory.
    /// These match the HWiNFO64 sensor shared memory format exactly.
    /// </summary>

    /// <summary>
    /// Header structure of the HWInfo64 shared memory.
    /// Contains metadata about the sensor data layout.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct HWiNFO_HEADER
    {
        /// <summary>Magic number identifying HWInfo64 format ('SiWH' or 'HWiN')</summary>
        public uint magic;

        /// <summary>HWInfo shared memory version</summary>
        public uint version;

        /// <summary>Secondary version identifier</summary>
        public uint version2;

        /// <summary>Unix timestamp of last update</summary>
        public long last_update;

        /// <summary>Offset to sensor section in bytes</summary>
        public uint sensor_section_offset;

        /// <summary>Size of each sensor element in bytes</summary>
        public uint sensor_element_size;

        /// <summary>Number of sensor elements</summary>
        public uint sensor_element_count;

        /// <summary>Offset to entry (reading) section in bytes</summary>
        public uint entry_section_offset;

        /// <summary>Size of each entry element in bytes</summary>
        public uint entry_element_size;

        /// <summary>Number of entry elements</summary>
        public uint entry_element_count;
    }

    /// <summary>
    /// Sensor structure from HWInfo64 shared memory.
    /// Contains sensor identification and naming information.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct HWiNFO_SENSOR
    {
        /// <summary>Unique sensor ID</summary>
        public uint id;

        /// <summary>Sensor instance number (for multiple sensors of same type)</summary>
        public uint instance;

        /// <summary>Original sensor name from HWInfo64</summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string name_original;

        /// <summary>User-customized sensor name (if set in HWInfo64)</summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string name_user;
    }

    /// <summary>
    /// Sensor type enumeration for HWInfo64 entries.
    /// </summary>
    public enum SensorType : uint
    {
        /// <summary>Unknown or no sensor type</summary>
        None = 0,

        /// <summary>Temperature sensor (°C, °F)</summary>
        Temperature = 1,

        /// <summary>Voltage sensor (V)</summary>
        Voltage = 2,

        /// <summary>Fan speed sensor (RPM)</summary>
        Fan = 3,

        /// <summary>Current sensor (A)</summary>
        Current = 4,

        /// <summary>Power sensor (W)</summary>
        Power = 5,

        /// <summary>Clock frequency sensor (MHz, GHz)</summary>
        Clock = 6,

        /// <summary>Usage/load percentage (%, 0-100)</summary>
        Usage = 7,

        /// <summary>Other sensor type</summary>
        Other = 8
    }

    /// <summary>
    /// Entry structure representing a single sensor reading.
    /// Each sensor can have multiple entries (min, max, current, etc.).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    public struct HWiNFO_ENTRY
    {
        /// <summary>Type of sensor reading</summary>
        public SensorType type;

        /// <summary>Index into the sensor array</summary>
        public uint sensor_index;

        /// <summary>Unique entry ID</summary>
        public uint id;

        /// <summary>Original entry name from HWInfo64</summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string name_original;

        /// <summary>User-customized entry name (if set in HWInfo64)</summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string name_user;

        /// <summary>Unit of measurement (e.g., "°C", "V", "RPM")</summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string unit;

        /// <summary>Current sensor value</summary>
        public double value;

        /// <summary>Minimum recorded value</summary>
        public double value_min;

        /// <summary>Maximum recorded value</summary>
        public double value_max;

        /// <summary>Average value (if tracked by HWInfo64)</summary>
        public double value_avg;
    }
}
