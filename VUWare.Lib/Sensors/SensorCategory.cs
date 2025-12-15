// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace VUWare.Lib.Sensors
{
    /// <summary>
    /// Defines common sensor categories/types across all sensor providers.
    /// This is a provider-agnostic enumeration that maps to provider-specific types.
    /// </summary>
    public enum SensorCategory
    {
        /// <summary>
        /// Unknown or unclassified sensor type.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Temperature sensor (typical units: °C, °F).
        /// </summary>
        Temperature = 1,

        /// <summary>
        /// Voltage sensor (typical units: V, mV).
        /// </summary>
        Voltage = 2,

        /// <summary>
        /// Fan speed sensor (typical units: RPM).
        /// </summary>
        Fan = 3,

        /// <summary>
        /// Current/amperage sensor (typical units: A, mA).
        /// </summary>
        Current = 4,

        /// <summary>
        /// Power consumption sensor (typical units: W).
        /// </summary>
        Power = 5,

        /// <summary>
        /// Clock/frequency sensor (typical units: MHz, GHz).
        /// </summary>
        Clock = 6,

        /// <summary>
        /// Usage/load percentage sensor (typical units: %).
        /// </summary>
        Load = 7,

        /// <summary>
        /// Data throughput sensor (typical units: MB/s, GB/s).
        /// </summary>
        Throughput = 8,

        /// <summary>
        /// Data size/capacity sensor (typical units: MB, GB, TB).
        /// </summary>
        Data = 9,

        /// <summary>
        /// Small data size sensor (typical units: KB, B).
        /// </summary>
        SmallData = 10,

        /// <summary>
        /// Control/PWM duty cycle (typical units: %).
        /// </summary>
        Control = 11,

        /// <summary>
        /// Level/percentage (typical units: %).
        /// </summary>
        Level = 12,

        /// <summary>
        /// Time duration sensor (typical units: s, ms).
        /// </summary>
        TimeSpan = 13,

        /// <summary>
        /// Factor/multiplier/ratio (no unit).
        /// </summary>
        Factor = 14,

        /// <summary>
        /// Frequency sensor - generic (typical units: Hz, kHz).
        /// </summary>
        Frequency = 15,

        /// <summary>
        /// Energy consumption sensor (typical units: Wh, kWh).
        /// </summary>
        Energy = 16,

        /// <summary>
        /// Noise level sensor (typical units: dB, dBA).
        /// </summary>
        Noise = 17
    }
}
