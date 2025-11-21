using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace VUWare.HWInfo64
{
    /// <summary>
    /// Reads HWInfo64 sensor data from shared memory.
    /// Handles the low-level access to HWInfo64's memory-mapped file.
    /// </summary>
    public class HWiNFOReader : IDisposable
    {
        private const string HWINFO_MAP_NAME = @"Global\HWiNFO_SENS_SM2";
        private MemoryMappedFile? _mmf;
        private MemoryMappedViewAccessor? _accessor;
        private bool _disposed;
        private bool _isConnected;

        public bool IsConnected => _isConnected;

        /// <summary>
        /// Attempts to connect to HWInfo64 shared memory.
        /// HWInfo64 must be running in Sensors-only mode with Shared Memory Support enabled.
        /// </summary>
        /// <returns>True if connection successful, false otherwise</returns>
        public bool Connect()
        {
            try
            {
                _mmf = MemoryMappedFile.OpenExisting(HWINFO_MAP_NAME);
                _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
                _isConnected = true;
                return true;
            }
            catch (Exception)
            {
                // HWInfo64 not running or Shared Memory Support not enabled
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Disconnects from HWInfo64 shared memory.
        /// </summary>
        public void Disconnect()
        {
            _accessor?.Dispose();
            _mmf?.Dispose();
            _accessor = null;
            _mmf = null;
            _isConnected = false;
        }

        /// <summary>
        /// Reads the HWInfo64 header from shared memory.
        /// </summary>
        /// <returns>Header structure or null if read fails</returns>
        public HWiNFO_HEADER? ReadHeader()
        {
            if (!_isConnected || _accessor == null)
                return null;

            try
            {
                return ReadStruct<HWiNFO_HEADER>(_accessor, 0);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Reads all sensors from shared memory.
        /// </summary>
        /// <returns>Array of sensor structures</returns>
        public HWiNFO_SENSOR[]? ReadAllSensors()
        {
            if (!_isConnected || _accessor == null)
                return null;

            try
            {
                var header = ReadStruct<HWiNFO_HEADER>(_accessor, 0);

                int sensorSize = (int)header.sensor_element_size;
                int sensorCount = (int)header.sensor_element_count;

                var sensors = new HWiNFO_SENSOR[sensorCount];

                for (int i = 0; i < sensorCount; i++)
                {
                    long offset = header.sensor_section_offset + i * sensorSize;
                    sensors[i] = ReadStruct<HWiNFO_SENSOR>(_accessor, offset);
                }

                return sensors;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Reads all entries (sensor readings) from shared memory.
        /// </summary>
        /// <returns>Array of entry structures</returns>
        public HWiNFO_ENTRY[]? ReadAllEntries()
        {
            if (!_isConnected || _accessor == null)
                return null;

            try
            {
                var header = ReadStruct<HWiNFO_HEADER>(_accessor, 0);

                int entrySize = (int)header.entry_element_size;
                int entryCount = (int)header.entry_element_count;

                var entries = new HWiNFO_ENTRY[entryCount];

                for (int i = 0; i < entryCount; i++)
                {
                    long offset = header.entry_section_offset + i * entrySize;
                    entries[i] = ReadStruct<HWiNFO_ENTRY>(_accessor, offset);
                }

                return entries;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Reads all sensor readings with metadata.
        /// This is the primary method for retrieving sensor data.
        /// </summary>
        /// <returns>List of SensorReading objects, or empty list if read fails</returns>
        public List<SensorReading> ReadAllSensorReadings()
        {
            var readings = new List<SensorReading>();

            if (!_isConnected || _accessor == null)
                return readings;

            try
            {
                var sensors = ReadAllSensors();
                var entries = ReadAllEntries();

                if (sensors == null || entries == null)
                    return readings;

                foreach (var entry in entries)
                {
                    // Skip null entries
                    if (entry.type == SensorType.None)
                        continue;

                    // Validate sensor index
                    if (entry.sensor_index >= sensors.Length)
                        continue;

                    var sensor = sensors[entry.sensor_index];

                    string sensorName = string.IsNullOrWhiteSpace(sensor.name_user)
                        ? sensor.name_original
                        : sensor.name_user;

                    string entryName = string.IsNullOrWhiteSpace(entry.name_user)
                        ? entry.name_original
                        : entry.name_user;

                    readings.Add(new SensorReading
                    {
                        SensorId = sensor.id,
                        SensorInstance = sensor.instance,
                        SensorName = sensorName,
                        EntryId = entry.id,
                        EntryName = entryName,
                        Type = entry.type,
                        Value = entry.value,
                        ValueMin = entry.value_min,
                        ValueMax = entry.value_max,
                        ValueAvg = entry.value_avg,
                        Unit = entry.unit,
                        LastUpdate = DateTime.Now
                    });
                }

                return readings;
            }
            catch
            {
                return readings;
            }
        }

        /// <summary>
        /// Helper method to read a struct from memory-mapped file.
        /// Handles marshaling of managed structures from unmanaged memory.
        /// </summary>
        private static T ReadStruct<T>(MemoryMappedViewAccessor accessor, long position) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            byte[] buffer = new byte[size];

            accessor.ReadArray(position, buffer, 0, size);

            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject()) is T value
                    ? value
                    : throw new InvalidOperationException("Failed to marshal struct");
            }
            finally
            {
                handle.Free();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Disconnect();
                _disposed = true;
            }
        }
    }
}
