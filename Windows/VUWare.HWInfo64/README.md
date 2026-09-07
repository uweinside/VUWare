# VUWare.HWInfo64 Library

A comprehensive C# library for reading HWInfo64 sensor values via shared memory and mapping them to VU1 dials for real-time display.

## Overview

The HWInfo64 library provides:
- **Low-level memory access** to HWInfo64 shared memory
- **Structured sensor data** with full type safety
- **High-level controller API** for easy integration
- **Automatic polling** with configurable intervals
- **VU1 dial integration** with threshold-based status indicators

## Prerequisites

- **HWInfo64** running in "Sensors-only" mode
- **Shared Memory Support** enabled in HWInfo64 settings
- .NET 8.0 or later

### Enabling Shared Memory in HWInfo64

1. Launch HWInfo64
2. Run in **"Sensors only"** mode (not Summary mode)
3. Go to **Options** ? **Shared Memory Support** ? Check "Enable Shared Memory Support"
4. Keep HWInfo64 running while your application reads sensors

## Architecture

### Core Components

#### HWiNFOReader
Low-level reader for accessing HWInfo64 shared memory directly.

```csharp
var reader = new HWiNFOReader();
if (reader.Connect())
{
    var readings = reader.ReadAllSensorReadings();
    foreach (var reading in readings)
    {
        Console.WriteLine($"{reading.SensorName}: {reading.Value} {reading.Unit}");
    }
    reader.Disconnect();
}
```

#### HWInfo64Controller
High-level controller with automatic polling and event notifications.

```csharp
var controller = new HWInfo64Controller();
controller.PollIntervalMs = 1000; // Poll every 1 second

// Register dial mappings
var cpuTempMapping = new DialSensorMapping
{
    Id = "cpu-temp",
    SensorName = "CPU Package",
    EntryName = "Temperature",
    MinValue = 20,
    MaxValue = 100,
    WarningThreshold = 80,
    CriticalThreshold = 95,
    DisplayName = "CPU Temp"
};
controller.RegisterDialMapping(cpuTempMapping);

// Connect and start polling
if (controller.Connect())
{
    // Handle sensor value changes
    controller.OnSensorValueChanged += (id, reading) => 
    {
        Console.WriteLine($"[{id}] {reading.Value} {reading.Unit}");
    };
    
    // Get current status
    var status = controller.GetSensorStatus("cpu-temp");
    Console.WriteLine($"Percentage: {status.Percentage}%");
    Console.WriteLine($"Critical: {status.IsCritical}");
    
    // Keep running...
    controller.Disconnect();
}
```

## Data Models

### SensorType Enumeration
Classifies sensor readings:
- `Temperature` - °C, °F
- `Voltage` - V
- `Fan` - RPM
- `Current` - A
- `Power` - W
- `Clock` - MHz, GHz
- `Usage` - % (0-100)
- `Other` - Misc sensors

### SensorReading
Represents a single sensor reading:

```csharp
public class SensorReading
{
    public uint SensorId { get; set; }
    public uint SensorInstance { get; set; }
    public string SensorName { get; set; }     // "CPU Package"
    public uint EntryId { get; set; }
    public string EntryName { get; set; }      // "Temperature"
    public SensorType Type { get; set; }       // SensorType.Temperature
    public double Value { get; set; }          // 45.3
    public double ValueMin { get; set; }       // 25.0
    public double ValueMax { get; set; }       // 87.5
    public double ValueAvg { get; set; }       // 62.1
    public string Unit { get; set; }           // "°C"
    public DateTime LastUpdate { get; set; }
}
```

### DialSensorMapping
Maps a HWInfo64 sensor to a VU1 dial with display ranges and thresholds:

```csharp
public class DialSensorMapping
{
    public string Id { get; set; }                    // Unique identifier
    public string SensorName { get; set; }            // "CPU Package"
    public string EntryName { get; set; }             // "Temperature"
    public double MinValue { get; set; }              // 20.0 ? 0% on dial
    public double MaxValue { get; set; }              // 100.0 ? 100% on dial
    public double? WarningThreshold { get; set; }     // 80.0
    public double? CriticalThreshold { get; set; }    // 95.0
    public string DisplayName { get; set; }           // "CPU Temp"
    
    // Helpers
    public byte GetPercentage(double value);
    public bool IsCritical(double value);
    public bool IsWarning(double value);
}
```

### SensorStatus
Current status of a mapped sensor for dial display:

```csharp
public class SensorStatus
{
    public string MappingId { get; set; }
    public SensorReading SensorReading { get; set; }
    public byte Percentage { get; set; }              // 0-100 for dial
    public bool IsCritical { get; set; }
    public bool IsWarning { get; set; }
    
    // Get recommended backlight color
    public (byte Red, byte Green, byte Blue) GetRecommendedColor();
    // Returns: Red (critical), Orange (warning), or Green (normal)
}
```

## Usage Examples

### Basic Sensor Reading

```csharp
using VUWare.HWInfo64;

var reader = new HWiNFOReader();
if (!reader.Connect())
{
    Console.WriteLine("HWInfo64 not running or Shared Memory Support disabled");
    return;
}

// Read header
var header = reader.ReadHeader();
Console.WriteLine($"HWInfo SM v{header.Value.version}");
Console.WriteLine($"Sensors: {header.Value.sensor_element_count}");
Console.WriteLine($"Entries: {header.Value.entry_element_count}");

// Read all sensor readings
var readings = reader.ReadAllSensorReadings();
foreach (var reading in readings.OrderBy(r => r.SensorName))
{
    Console.WriteLine($"{reading.SensorName}");
    Console.WriteLine($"  {reading.EntryName}: {reading.Value:0.##} {reading.Unit}");
    Console.WriteLine($"  Min: {reading.ValueMin:0.##}, Max: {reading.ValueMax:0.##}");
}

reader.Disconnect();
```

### VU1 Integration Example

```csharp
using VUWare.Lib;
using VUWare.HWInfo64;

// Initialize VU1 controller
var vuController = new VU1Controller();
vuController.AutoDetectAndConnect();
await vuController.InitializeAsync();

// Initialize HWInfo64 controller
var hwController = new HWInfo64Controller();
hwController.PollIntervalMs = 500; // Update every 500ms

// Register CPU temperature mapping to a dial
var cpuTempMapping = new DialSensorMapping
{
    Id = "dial-cpu-temp",
    SensorName = "CPU Package",
    EntryName = "Temperature",
    MinValue = 20,
    MaxValue = 100,
    WarningThreshold = 80,
    CriticalThreshold = 95,
    DisplayName = "CPU Temperature"
};
hwController.RegisterDialMapping(cpuTempMapping);

// Get reference to the dial
var dialUID = "YOUR_DIAL_UID_HERE";
var dial = vuController.GetDial(dialUID);

if (hwController.Connect())
{
    hwController.OnSensorValueChanged += async (mappingId, reading) =>
    {
        if (mappingId == "dial-cpu-temp")
        {
            // Get the percentage for the dial
            var percentage = hwController.GetDialPercentage(mappingId);
            if (percentage.HasValue)
            {
                // Update dial position
                await vuController.SetDialPercentageAsync(dialUID, percentage.Value);
                
                // Update backlight color based on threshold
                var status = hwController.GetSensorStatus(mappingId);
                var (r, g, b) = status.GetRecommendedColor();
                await vuController.SetBacklightAsync(dialUID, r, g, b);
            }
        }
    };
    
    // Keep running...
    Console.WriteLine("HWInfo64 monitoring active. Press any key to exit...");
    Console.ReadKey();
    
    hwController.Disconnect();
}

vuController.Dispose();
```

### Finding Sensor Names

To find available sensor and entry names in HWInfo64:

```csharp
var reader = new HWiNFOReader();
if (reader.Connect())
{
    var readings = reader.ReadAllSensorReadings();
    
    // Group by sensor
    var grouped = readings.GroupBy(r => r.SensorName);
    
    foreach (var sensorGroup in grouped)
    {
        Console.WriteLine($"\n{sensorGroup.Key}:");
        foreach (var reading in sensorGroup)
        {
            Console.WriteLine($"  • {reading.EntryName} ({reading.Type})");
        }
    }
    
    reader.Disconnect();
}
```

Common sensor names:
- `CPU Package` - Overall CPU temperature
- `CPU Core #0`, `CPU Core #1`, etc. - Individual core temperatures
- `GPU` - GPU temperature
- `System` - System temperature
- `Motherboard` - Mobo temperature

Common entry names:
- `Temperature` - Current temperature
- `Clock` - Current frequency
- `Load` or `Usage` - Current utilization

## Advanced Features

### Custom Polling Intervals

```csharp
var controller = new HWInfo64Controller();
controller.PollIntervalMs = 1000;  // Poll every second (min: 100ms)
controller.Connect();
```

### Event Handling

```csharp
controller.OnSensorValueChanged += (id, reading) =>
{
    Console.WriteLine($"[{id}] Changed to {reading.Value}");
};
```

### Snapshot Analysis

```csharp
var snapshot = new SensorSnapshot
{
    Timestamp = DateTime.Now,
    Readings = reader.ReadAllSensorReadings()
};

// Find specific reading
var cpuTemp = snapshot.FindReading("CPU Package", "Temperature");

// Get all temperature readings
var temperatures = snapshot.GetReadingsByType(SensorType.Temperature);
```

## Troubleshooting

### "HWInfo64 not running or Shared Memory Support disabled"

**Solution:**
1. Launch HWInfo64
2. Select "Sensors only" mode (not Summary)
3. Open Options ? Enable "Shared Memory Support"
4. Ensure HWInfo64 remains running

### No Sensor Data Retrieved

**Checklist:**
- [ ] HWInfo64 is running
- [ ] Running in "Sensors only" mode
- [ ] "Shared Memory Support" is enabled in Options
- [ ] Correct sensor/entry names are being used
- [ ] System has at least one sensor detected

### Wrong Sensor Names

Use the discovery example above to print all available sensors and entry names.

### Dial Not Updating

- Verify VU1 dial UID is correct
- Check poll interval is not too high
- Ensure HWInfo64 is still reading sensors
- Verify mapping min/max values match sensor data range

## Performance Considerations

- **Polling Interval:** Default 500ms is suitable for most uses. Can go as low as 100ms, but minimal benefit with HWInfo64 update rates
- **Number of Mappings:** Can register unlimited mappings; only registered ones are tracked
- **Memory Usage:** Minimal; structures are small and reused
- **CPU Usage:** Negligible; mostly I/O wait on memory-mapped file

## API Reference

### HWiNFOReader
- `bool Connect()` - Connect to HWInfo64 shared memory
- `void Disconnect()` - Disconnect and clean up
- `HWiNFO_HEADER? ReadHeader()` - Read header metadata
- `HWiNFO_SENSOR[]? ReadAllSensors()` - Read all sensors
- `HWiNFO_ENTRY[]? ReadAllEntries()` - Read all entries
- `List<SensorReading> ReadAllSensorReadings()` - Read all readings (high-level)

### HWInfo64Controller
- `bool Connect()` - Connect and start polling
- `void Disconnect()` - Stop polling and disconnect
- `void RegisterDialMapping(DialSensorMapping)` - Register a dial mapping
- `void UnregisterDialMapping(string)` - Unregister a mapping
- `IReadOnlyDictionary<string, DialSensorMapping> GetAllMappings()` - Get all mappings
- `SensorReading? GetCurrentReading(string mappingId)` - Get last reading for mapping
- `List<SensorReading> GetAllSensorReadings()` - Get all current sensor readings
- `byte? GetDialPercentage(string mappingId)` - Get dial percentage for mapping
- `SensorStatus? GetSensorStatus(string mappingId)` - Get full status for mapping
- `event SensorValueChanged OnSensorValueChanged` - Fired when sensor value changes

## License

Part of the VUWare project. See main repository for license details.

## References

- [HWInfo64 Official Site](https://www.hwinfo.com/)
- [VUWare GitHub](https://github.com/uweinside/VUWare)
- [VU1 Gauge Hub](https://vudials.com/)
