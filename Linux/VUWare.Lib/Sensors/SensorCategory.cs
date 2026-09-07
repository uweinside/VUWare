// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace VUWare.Lib.Sensors;

/// <summary>Defines provider-independent sensor categories.</summary>
public enum SensorCategory
{
    Unknown = 0,
    Temperature = 1,
    Voltage = 2,
    Fan = 3,
    Current = 4,
    Power = 5,
    Clock = 6,
    Load = 7,
    Throughput = 8,
    Data = 9,
    SmallData = 10,
    Control = 11,
    Level = 12,
    TimeSpan = 13,
    Factor = 14,
    Frequency = 15,
    Energy = 16,
    Noise = 17
}
