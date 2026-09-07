# HWInfo64 Library Quick Reference

## 30-Second Setup

```csharp
using VUWare.HWInfo64;

// 1. Create controller
var controller = new HWInfo64Controller();

// 2. Register a dial mapping
controller.RegisterDialMapping(new DialSensorMapping
{
    Id = "cpu-temp",
    SensorName = "CPU Package",
    EntryName = "Temperature",
    MinValue = 20,      // 0% on dial
    MaxValue = 100,     // 100% on dial
    WarningThreshold = 80,
    CriticalThreshold = 95
});

// 3. Connect and start polling
controller.Connect();

// 4. Get sensor data
var status = controller.GetSensorStatus("cpu-temp");
Console.WriteLine($"CPU: {status.Percentage}% - {status.SensorReading.Value}°C");

// 5. Cleanup
controller.Disconnect();
```

## Finding Sensor Names

HWInfo64 sensor/entry names vary by hardware. Common examples:

### CPUs
- **Sensor:** "CPU Package", "CPU Core #0", "CPU Core #1"
- **Entries:** "Temperature", "Clock", "Power", "Load"

### GPUs
- **Sensor:** "GPU" (NVIDIA) or "GPU Core" (AMD)
- **Entries:** "Temperature", "Clock", "Memory Clock", "Memory Used", "Load"

### Storage
- **Sensor:** "HDD SSD Name" (varies per device)
- **Entries:** "Temperature"

### Motherboard
- **Sensor:** "Motherboard", "System"
- **Entries:** "Temperature"

### Fans
- **Sensor:** "Fan #1", "Fan #2", etc.
- **Entries:** "Speed"

To discover all available sensors on your system:
```csharp
var reader = new HWiNFOReader();
reader.Connect();
var readings = reader.ReadAllSensorReadings();
foreach (var reading in readings)
    Console.WriteLine($"{reading.SensorName} > {reading.EntryName}");
reader.Disconnect();
```

## Mapping Configuration Examples

### Temperature Dial
```csharp
new DialSensorMapping
{
    Id = "dial-1",
    SensorName = "CPU Package",
    EntryName = "Temperature",
    MinValue = 20,
    MaxValue = 100,
    WarningThreshold = 85,
    CriticalThreshold = 95
}
```

### Fan Speed Dial
```csharp
new DialSensorMapping
{
    Id = "dial-2",
    SensorName = "Fan #1",
    EntryName = "Speed",
    MinValue = 0,
    MaxValue = 3000,        // RPM
    WarningThreshold = 2500
}
```

### GPU Load Dial
```csharp
new DialSensorMapping
{
    Id = "dial-3",
    SensorName = "GPU",
    EntryName = "Load",
    MinValue = 0,
    MaxValue = 100,         // Already percentage
    WarningThreshold = 80,
    CriticalThreshold = 95
}
```

### Memory Usage Dial
```csharp
new DialSensorMapping
{
    Id = "dial-4",
    SensorName = "HWiNFO_SHARED_MEM",
    EntryName = "Memory Used",
    MinValue = 0,
    MaxValue = 32,          // GB (adjust for your system)
    WarningThreshold = 24
}
```

## Reading Current Values

```csharp
// Get percentage for dial display
byte? percentage = controller.GetDialPercentage("dial-1");
// Result: 0-100

// Get complete status with thresholds
var status = controller.GetSensorStatus("dial-1");
// status.Percentage       - 0-100 for dial
// status.IsCritical       - bool
// status.IsWarning        - bool
// status.SensorReading    - Full reading data

// Get recommended backlight color
var (red, green, blue) = status.GetRecommendedColor();
// Green (0,100,0) = normal
// Orange (100,50,0) = warning
// Red (100,0,0) = critical

// Get raw sensor value
var reading = controller.GetCurrentReading("dial-1");
Console.WriteLine($"{reading.Value} {reading.Unit}");
```

## Event Handling

```csharp
controller.OnSensorValueChanged += (mappingId, reading) =>
{
    Console.WriteLine($"{mappingId}: {reading.Value}");
};

// Events fire when:
// 1. Mapping is registered
// 2. Sensor value changes (by >0.01 threshold)
// 3. During polling cycle
```

## Configuration

### Polling Interval
```csharp
controller.PollIntervalMs = 500;    // 500ms (default)
controller.PollIntervalMs = 1000;   // 1 second
controller.PollIntervalMs = 100;    // 100ms (minimum)
```

## Thresholds Guide

### CPU Temperature
```
MinValue: 20    (room temp)
MaxValue: 100   (throttle temp)
Warning: 85     (safe operation)
Critical: 95    (thermal throttling)
```

### GPU Temperature
```
MinValue: 20
MaxValue: 90
Warning: 80
Critical: 85
```

### Fan Speed
```
MinValue: 0
MaxValue: 3000  (max RPM)
Warning: 2500   (high speed)
Critical: 3000  (max speed)
```

### CPU Load
```
MinValue: 0
MaxValue: 100
Warning: 80
Critical: 95
```

## Integration with VU1 Dials

```csharp
var vuController = new VU1Controller();
var hwController = new HWInfo64Controller();

// Register mapping
hwController.RegisterDialMapping(mapping);

// Handle updates
hwController.OnSensorValueChanged += async (id, reading) =>
{
    var status = hwController.GetSensorStatus(id);
    var (r, g, b) = status.GetRecommendedColor();
    
    await vuController.SetDialPercentageAsync(dialUID, status.Percentage);
    await vuController.SetBacklightAsync(dialUID, r, g, b);
};

hwController.Connect();
```

## Troubleshooting Checklist

- [ ] HWInfo64 is running in "Sensors only" mode
- [ ] "Shared Memory Support" is enabled (Options menu)
- [ ] Sensor name matches exactly (case-insensitive but spelling matters)
- [ ] Entry name matches exactly (case-insensitive but spelling matters)
- [ ] MinValue < MaxValue
- [ ] Using correct dial UID when updating VU1

## Properties Reference

### SensorReading
- `SensorName` - Name of the sensor
- `EntryName` - Name of the specific reading
- `Type` - SensorType enum (Temperature, Voltage, Fan, etc.)
- `Value` - Current sensor value
- `ValueMin` - Minimum recorded value
- `ValueMax` - Maximum recorded value
- `ValueAvg` - Average value
- `Unit` - Unit of measurement ("°C", "V", "RPM", etc.)
- `LastUpdate` - DateTime of last update

### DialSensorMapping
- `Id` - Unique identifier string
- `SensorName` - HWInfo64 sensor name
- `EntryName` - HWInfo64 entry name
- `MinValue` - Value that maps to 0% on dial
- `MaxValue` - Value that maps to 100% on dial
- `WarningThreshold` - Optional warning level
- `CriticalThreshold` - Optional critical level
- `DisplayName` - User-friendly display name

### SensorStatus
- `MappingId` - The mapping ID
- `SensorReading` - Current sensor reading
- `Percentage` - 0-100 for dial display
- `IsCritical` - true if >= critical threshold
- `IsWarning` - true if >= warning threshold (but not critical)

## Common Sensor Names by Hardware

### Intel CPUs
- "CPU Package" - Overall temperature
- "CPU Core #0" - Individual core temps
- "Package Power" - Power draw

### AMD CPUs
- "Core #0-0" - Core temperatures  
- "CPU Core" - Average core temp
- "Thermal Throttle" - Throttling status

### NVIDIA GPUs
- "GPU" - GPU temperature
- "GPU Memory" - VRAM temperature
- "GPU Core" - Core frequency

### AMD GPUs
- "GPU Core" - Core temperature
- "HBM" - Memory temperature

### Motherboards
- "Motherboard" - Mobo temp
- "System" - System temp
- "CPU Fan", "System Fan", "Case Fan", etc. - Fan speeds
