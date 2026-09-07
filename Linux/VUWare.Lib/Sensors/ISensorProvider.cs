// Copyright (c) 2025 Uwe Baumann
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace VUWare.Lib.Sensors;

/// <summary>Provides hardware measurements through a platform-independent contract.</summary>
public interface ISensorProvider : IDisposable
{
    string ProviderName { get; }
    bool IsConnected { get; }
    bool Connect();
    void Disconnect();
    IReadOnlyList<ISensorDescriptor> GetSensors();
    IReadOnlyList<ISensorReading> GetAllReadings();
    IReadOnlyList<ISensorReading> GetReadingsByType(SensorCategory type);
    ISensorReading? GetReading(string sensorName, string entryName);
    IReadOnlyList<SensorCategory> GetAvailableCategories();
    event EventHandler<SensorProviderConnectionEventArgs>? ConnectionStatusChanged;
}

public interface ISensorDescriptor
{
    uint Id { get; }
    uint Instance { get; }
    string Name { get; }
    string OriginalName { get; }
}

public sealed class SensorProviderConnectionEventArgs(bool isConnected, string? message = null) : EventArgs
{
    public bool IsConnected { get; } = isConnected;
    public string? Message { get; } = message;
}
