// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace VUWare.Lib.Sensors
{
    /// <summary>
    /// Provides dial-to-sensor mapping functionality that works with any ISensorProvider.
    /// This abstraction decouples dial mapping from specific sensor providers.
    /// </summary>
    public interface IDialMappingService : IDisposable
    {
        /// <summary>
        /// Gets the underlying sensor provider.
        /// </summary>
        ISensorProvider SensorProvider { get; }

        /// <summary>
        /// Gets whether the service is connected and able to retrieve sensor data.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Registers a dial-to-sensor mapping.
        /// </summary>
        /// <param name="mapping">The mapping configuration.</param>
        void RegisterMapping(DialSensorMapping mapping);

        /// <summary>
        /// Registers multiple dial-to-sensor mappings.
        /// </summary>
        /// <param name="mappings">Collection of mapping configurations.</param>
        void RegisterMappings(IEnumerable<DialSensorMapping> mappings);

        /// <summary>
        /// Unregisters a dial mapping.
        /// </summary>
        /// <param name="mappingId">The mapping ID to remove.</param>
        void UnregisterMapping(string mappingId);

        /// <summary>
        /// Clears all registered mappings.
        /// </summary>
        void ClearAllMappings();

        /// <summary>
        /// Gets all registered mappings.
        /// </summary>
        IReadOnlyDictionary<string, DialSensorMapping> GetAllMappings();

        /// <summary>
        /// Gets the current status for a mapped dial.
        /// </summary>
        /// <param name="mappingId">The mapping ID (dial UID).</param>
        /// <returns>The current status, or null if mapping not found or no data available.</returns>
        DialSensorStatus? GetStatus(string mappingId);

        /// <summary>
        /// Gets the current sensor reading for a mapped dial.
        /// </summary>
        /// <param name="mappingId">The mapping ID (dial UID).</param>
        /// <returns>The sensor reading, or null if not found.</returns>
        ISensorReading? GetReading(string mappingId);

        /// <summary>
        /// Updates the poll interval for sensor readings.
        /// </summary>
        /// <param name="intervalMs">Interval in milliseconds (minimum 100ms).</param>
        void UpdatePollInterval(int intervalMs);

        /// <summary>
        /// Event raised when a sensor value changes for a registered mapping.
        /// </summary>
        event Action<string, ISensorReading>? OnSensorValueChanged;
    }
}
