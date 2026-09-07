// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace VUWare.Lib.Sensors;

/// <summary>Represents one point-in-time sensor measurement.</summary>
public interface ISensorReading
{
    uint SensorId { get; }
    uint SensorInstance { get; }
    string SensorName { get; }
    uint EntryId { get; }
    string EntryName { get; }
    SensorCategory Category { get; }
    double Value { get; }
    double ValueMin { get; }
    double ValueMax { get; }
    double ValueAvg { get; }
    string Unit { get; }
    DateTime LastUpdate { get; }
}
