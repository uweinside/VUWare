// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace VUWare.Lib.Sensors
{
    /// <summary>
    /// Provides an abstraction for sensor data providers (HWInfo64, OpenHardwareMonitor, AIDA64, etc.).
    /// Implementations read sensor data from specific monitoring software and expose it through a common interface.
    /// </summary>
    public interface ISensorProvider : IDisposable
    {
        /// <summary>
        /// Gets the display name of this sensor provider (e.g., "HWInfo64", "OpenHardwareMonitor").
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Gets whether the provider is currently connected and able to read sensor data.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Attempts to connect to the sensor data source.
        /// </summary>
        /// <returns>True if connection was successful, false otherwise.</returns>
        bool Connect();

        /// <summary>
        /// Disconnects from the sensor data source.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Gets all available sensors from the provider.
        /// A sensor represents a hardware component or logical grouping (e.g., "CPU Package", "GPU Core").
        /// </summary>
        /// <returns>Collection of sensor descriptors.</returns>
        IReadOnlyList<ISensorDescriptor> GetSensors();

        /// <summary>
        /// Gets all sensor entries (individual readings) from the provider.
        /// An entry is a specific measurement from a sensor (e.g., "Temperature", "Load", "Clock").
        /// </summary>
        /// <returns>Collection of sensor entries with current values.</returns>
        IReadOnlyList<ISensorReading> GetAllReadings();

        /// <summary>
        /// Gets sensor readings filtered by sensor type.
        /// </summary>
        /// <param name="type">The type of sensors to retrieve.</param>
        /// <returns>Collection of sensor readings matching the specified type.</returns>
        IReadOnlyList<ISensorReading> GetReadingsByType(SensorCategory type);

        /// <summary>
        /// Gets a specific sensor reading by sensor name and entry name.
        /// </summary>
        /// <param name="sensorName">Name of the sensor (e.g., "CPU Package").</param>
        /// <param name="entryName">Name of the entry (e.g., "Temperature").</param>
        /// <returns>The sensor reading if found, null otherwise.</returns>
        ISensorReading? GetReading(string sensorName, string entryName);

        /// <summary>
        /// Gets all available sensor categories/types supported by this provider.
        /// </summary>
        /// <returns>Collection of available sensor categories.</returns>
        IReadOnlyList<SensorCategory> GetAvailableCategories();

        /// <summary>
        /// Event raised when connection status changes.
        /// </summary>
        event EventHandler<SensorProviderConnectionEventArgs>? ConnectionStatusChanged;
    }

    /// <summary>
    /// Describes a sensor (hardware component or logical grouping that contains multiple readings).
    /// </summary>
    public interface ISensorDescriptor
    {
        /// <summary>
        /// Gets the unique identifier for this sensor.
        /// </summary>
        uint Id { get; }

        /// <summary>
        /// Gets the instance number (for sensors with same ID on different hardware instances).
        /// </summary>
        uint Instance { get; }

        /// <summary>
        /// Gets the display name of the sensor.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the original/internal name of the sensor (before any user customization).
        /// </summary>
        string OriginalName { get; }
    }

    /// <summary>
    /// Event arguments for sensor provider connection status changes.
    /// </summary>
    public class SensorProviderConnectionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets whether the provider is now connected.
        /// </summary>
        public bool IsConnected { get; }

        /// <summary>
        /// Gets an optional message describing the connection change.
        /// </summary>
        public string? Message { get; }

        public SensorProviderConnectionEventArgs(bool isConnected, string? message = null)
        {
            IsConnected = isConnected;
            Message = message;
        }
    }
}
