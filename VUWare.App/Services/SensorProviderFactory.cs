// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using VUWare.App.Models;
using VUWare.AIDA64;
using VUWare.HWInfo64;
using VUWare.Lib.Sensors;

namespace VUWare.App.Services
{
    /// <summary>
    /// Factory for creating sensor provider instances based on configuration.
    /// Supports multiple sensor data sources (HWInfo64, AIDA64, etc.).
    /// </summary>
    public static class SensorProviderFactory
    {
        /// <summary>
        /// Creates a sensor provider instance based on the configured provider type.
        /// </summary>
        /// <param name="providerType">The type of sensor provider to create.</param>
        /// <returns>An ISensorProvider instance, or null if the provider is not supported.</returns>
        /// <exception cref="NotSupportedException">Thrown when the provider type is not yet implemented.</exception>
        public static ISensorProvider Create(SensorProviderType providerType)
        {
            System.Diagnostics.Debug.WriteLine($"[SensorProviderFactory] Creating provider: {providerType}");

            return providerType switch
            {
                SensorProviderType.HWInfo64 => CreateHWInfo64Provider(),
                SensorProviderType.AIDA64 => CreateAIDA64Provider(),
                SensorProviderType.LibreHardwareMonitor => throw new NotSupportedException(
                    "LibreHardwareMonitor provider is not yet implemented. " +
                    "Please use HWInfo64 or AIDA64 instead."),
                _ => throw new ArgumentOutOfRangeException(nameof(providerType), 
                    $"Unknown sensor provider type: {providerType}")
            };
        }

        /// <summary>
        /// Creates a sensor provider and attempts to connect to it.
        /// </summary>
        /// <param name="providerType">The type of sensor provider to create.</param>
        /// <param name="provider">The created provider instance (output).</param>
        /// <returns>True if provider was created and connected successfully.</returns>
        public static bool TryCreateAndConnect(SensorProviderType providerType, out ISensorProvider? provider)
        {
            provider = null;

            try
            {
                provider = Create(providerType);
                bool connected = provider.Connect();

                if (!connected)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[SensorProviderFactory] Failed to connect to {providerType}");
                    provider.Dispose();
                    provider = null;
                    return false;
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[SensorProviderFactory] Successfully connected to {providerType}");
                return true;
            }
            catch (NotSupportedException ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SensorProviderFactory] Provider not supported: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SensorProviderFactory] Error creating provider: {ex.Message}");
                provider?.Dispose();
                provider = null;
                return false;
            }
        }

        /// <summary>
        /// Gets the display name for a provider type.
        /// </summary>
        public static string GetProviderDisplayName(SensorProviderType providerType)
        {
            return providerType switch
            {
                SensorProviderType.HWInfo64 => "HWInfo64",
                SensorProviderType.AIDA64 => "AIDA64",
                SensorProviderType.LibreHardwareMonitor => "LibreHardwareMonitor",
                _ => providerType.ToString()
            };
        }

        /// <summary>
        /// Gets a description of requirements for a provider type.
        /// </summary>
        public static string GetProviderRequirements(SensorProviderType providerType)
        {
            return providerType switch
            {
                SensorProviderType.HWInfo64 => 
                    "HWInfo64 must be running in 'Sensors-only' mode with " +
                    "'Shared Memory Support' enabled in Settings ? Safety.",
                SensorProviderType.AIDA64 => 
                    "AIDA64 must be running with shared memory enabled: " +
                    "File ? Preferences ? External Applications ? Shared Memory ? Enable Shared Memory.",
                SensorProviderType.LibreHardwareMonitor => 
                    "LibreHardwareMonitor support is not yet implemented.",
                _ => "Unknown provider requirements."
            };
        }

        /// <summary>
        /// Checks if a provider type is currently supported/implemented.
        /// </summary>
        public static bool IsProviderSupported(SensorProviderType providerType)
        {
            return providerType switch
            {
                SensorProviderType.HWInfo64 => true,
                SensorProviderType.AIDA64 => true,
                SensorProviderType.LibreHardwareMonitor => false, // Not yet implemented
                _ => false
            };
        }

        /// <summary>
        /// Gets all available (implemented) provider types.
        /// </summary>
        public static IReadOnlyList<SensorProviderType> GetAvailableProviders()
        {
            return Enum.GetValues<SensorProviderType>()
                .Where(IsProviderSupported)
                .ToList();
        }

        private static ISensorProvider CreateHWInfo64Provider()
        {
            System.Diagnostics.Debug.WriteLine("[SensorProviderFactory] Creating HWInfo64SensorProvider");
            return new HWInfo64SensorProvider();
        }

        private static ISensorProvider CreateAIDA64Provider()
        {
            System.Diagnostics.Debug.WriteLine("[SensorProviderFactory] Creating AIDA64SensorProvider");
            return new AIDA64SensorProvider();
        }
    }
}
