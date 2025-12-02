# VUWare Architecture: Sensor Monitoring & Physical Dial Control

## Overview

VUWare is a .NET 8 WPF application that reads hardware sensor data from HWInfo64 and displays it on physical VU1 analog dials. The architecture is optimized for real-time responsiveness, efficient resource usage, and production-ready reliability.

---

## System Components

```
???????????????????????????????????????????????????????????????????
?                         VUWare Application                      ?
???????????????????????????????????????????????????????????????????
?                                                                 ?
?  ????????????????????          ????????????????????           ?
?  ?  WPF UI Layer    ????????????  System Tray     ?           ?
?  ?  (MainWindow)    ?          ?  Manager         ?           ?
?  ????????????????????          ????????????????????           ?
?           ?                                                     ?
?           ?                                                     ?
?  ????????????????????????????????????????????????????         ?
?  ?        AppInitializationService                   ?         ?
?  ?  • VU1 Hub connection                            ?         ?
?  ?  • HWInfo64 connection (with retry)              ?         ?
?  ?  • Dial discovery & provisioning                 ?         ?
?  ????????????????????????????????????????????????????         ?
?              ?                            ?                    ?
?              ?                            ?                    ?
?  ??????????????????????      ??????????????????????????       ?
?  ?  VU1Controller     ?      ?  HWInfo64Controller    ?       ?
?  ?  (VUWare.Lib)      ?      ?  (VUWare.HWInfo64)     ?       ?
?  ??????????????????????      ??????????????????????????       ?
?            ?                           ?                       ?
?            ?                           ?                       ?
?  ??????????????????????      ??????????????????????????       ?
?  ?  DeviceManager     ?      ?  HWiNFOReader          ?       ?
?  ?  • Dial management ?      ?  • Shared memory read  ?       ?
?  ?  • Command queue   ?      ?  • 100ms timeout       ?       ?
?  ??????????????????????      ??????????????????????????       ?
?            ?                           ?                       ?
?            ?                           ?                       ?
?  ??????????????????????               ?                       ?
?  ? SerialPortManager  ?               ?                       ?
?  ?  • Async I/O       ?               ?                       ?
?  ?  • USB/Serial comm ?               ?                       ?
?  ??????????????????????               ?                       ?
?            ?                           ?                       ?
??????????????????????????????????????????????????????????????????
             ?                           ?
             ?                           ?
    ??????????????????         ????????????????????
    ?  VU1 Gauge Hub ?         ?  HWInfo64        ?
    ?  (USB Serial)  ?         ?  (Shared Memory) ?
    ??????????????????         ????????????????????
             ?                          ?
             ?                          ?
    ??????????????????         ????????????????????
    ? Physical Dials ?         ?  Hardware        ?
    ? (I2C bus)      ?         ?  Sensors         ?
    ??????????????????         ????????????????????
```

---

## Data Flow Architecture

### Sensor Reading Pipeline

```
Hardware Sensors
      ?
HWInfo64 (reads via MSR, SMBus, etc.)
      ?
HWInfo64 Internal Buffer
      ?
Shared Memory Writer Thread ? Memory-Mapped File
                              "Global\HWiNFO_SENS_SM2"
      ?
HWiNFOReader.ReadAllSensorReadings()
(100ms timeout protection)
      ?
Cache Layer (last known values)
      ?
HWInfo64Controller.PollingLoop()
(Dedicated thread, AboveNormal priority)
      ?
SensorMonitoringService.UpdateDialAsync()
(Dedicated thread, AboveNormal priority)
      ?
VU1Controller.SetDialPercentageAsync()
      ?
DeviceManager ? SerialPortManager
(Async I/O)
      ?
VU1 Gauge Hub ? Physical Dial Movement
```

---

## Threading Model

### Dedicated Threads for Time-Critical Operations

The application uses dedicated threads with elevated priority for sensor polling and monitoring to ensure consistent performance:

#### HWInfo64 Polling Thread
```csharp
// VUWare.HWInfo64\HWInfo64Controller.cs
private void StartPolling()
{
    var pollingThread = new Thread(() => PollingLoop(_pollingCancellation.Token))
    {
        Name = "HWInfo64 Polling",
        IsBackground = true,
        Priority = ThreadPriority.AboveNormal
    };
    pollingThread.Start();
}
```

Polls HWInfo64 shared memory every 500ms to retrieve sensor data.

#### Sensor Monitoring Thread
```csharp
// VUWare.App\Services\SensorMonitoringService.cs
public void Start()
{
    var monitoringThread = new Thread(async () =>
    {
        await InitializeDials(_monitoringCts.Token);
        MonitoringLoop(_monitoringCts.Token);
    })
    {
        Name = "Sensor Monitoring",
        IsBackground = true,
        Priority = ThreadPriority.AboveNormal
    };
    monitoringThread.Start();
}
```

Processes sensor updates and controls physical dial positions.

#### Async I/O Operations
All serial port operations use true async I/O to avoid blocking:

```csharp
// VUWare.Lib\SerialPortManager.cs
private async Task<string> ReadResponseAsync(int timeoutMs, CancellationToken cancellationToken)
{
    int bytesRead = await _serialPort!.BaseStream.ReadAsync(buffer, 0, buffer.Length, cts.Token);
    
    if (bytesRead == 0)
    {
        emptyReadCount++;
        if (emptyReadCount >= 3)
        {
            await Task.Yield();
        }
    }
}
```

---

## Key Design Features

### 1. Timeout Protection on Shared Memory Reads

Shared memory reads have 100ms timeout protection to prevent blocking:

```csharp
// VUWare.HWInfo64\HWiNFOReader.cs
public List<SensorReading> ReadAllSensorReadings()
{
    var readTask = Task.Run(() => {
        var sensors = ReadAllSensors();
        var entries = ReadAllEntries();
        return ProcessReadings(sensors, entries);
    });
    
    if (readTask.Wait(100))
    {
        return readTask.Result;
    }
    else
    {
        return new List<SensorReading>();  // Timeout - use cached data
    }
}
```

### 2. Data Caching Layer

A cache layer stores the last known sensor values to ensure continuous dial operation:

```csharp
// VUWare.App\Services\SensorMonitoringService.cs
private readonly Dictionary<string, SensorReading> _lastKnownReadings = new();

private async Task<bool> UpdateDialAsync(DialMonitoringState state, CancellationToken cancellationToken)
{
    var status = _hwInfoController.GetSensorStatus(state.DialUid);
    
    if (status == null && _lastKnownReadings.TryGetValue(state.DialUid, out var cachedReading))
    {
        status = CreateStatusFromCache(cachedReading);
    }
    else if (status != null)
    {
        _lastKnownReadings[state.DialUid] = status.SensorReading!;
    }
    
    await UpdatePhysicalDial(state, status);
}
```

### 3. Efficient Async I/O

Serial port operations yield cooperatively rather than busy-waiting:

```csharp
if (bytesRead == 0)
{
    emptyReadCount++;
    if (emptyReadCount >= 3)
    {
        await Task.Yield();  // Cooperative yielding
    }
}
```

### 4. Conditional UI Updates

UI updates are disabled when the window is minimized to reduce overhead:

```csharp
// VUWare.App\Services\SensorMonitoringService.cs
private bool _enableUIUpdates = true;

public void SetUIUpdateEnabled(bool enabled)
{
    _enableUIUpdates = enabled;
}

private void RaiseDialUpdated(DialMonitoringState state)
{
    if (!_enableUIUpdates) return;
    
    Application.Current?.Dispatcher?.Invoke(() =>
    {
        OnDialUpdated?.Invoke(state.DialUid, update);
    });
}
```

---

## Configuration

### Sensor Mapping Configuration

```json
// VUWare.App\Config\dials-config.json
{
  "dials": [
    {
      "dialUid": "290063000750524834313020",
      "displayName": "CPU Usage",
      "enabled": true,
      "sensorName": "CPU [#0]: AMD Ryzen 7 9700X",
      "entryName": "Total CPU Usage",
      "minValue": 0,
      "maxValue": 100,
      "warningThreshold": 80,
      "criticalThreshold": 95,
      "colorConfig": {
        "normalColor": "Cyan",
        "warningColor": "Yellow",
        "criticalColor": "Red"
      }
    }
  ],
  "appSettings": {
    "globalUpdateIntervalMs": 500,
    "debugMode": false
  }
}
```

### HWInfo64 Configuration

**Recommended Settings:**

1. **Polling Period:** 500ms
   - HWInfo64 ? Sensors ? Configure ? General ? Polling Period: 500

2. **Shared Memory:** Enabled
   - HWInfo64 ? Settings ? General ? ? Shared Memory Support

3. **Process Priority:** Above Normal (optional)
   - Task Manager ? Details ? HWiNFO64.exe ? Set Priority ? Above Normal

---

## Performance Characteristics

### Normal Operation

| Metric | Value |
|--------|-------|
| **CPU Usage** | <1% |
| **Sensor Read Latency** | 100-500ms |
| **Dial Update Latency** | 100-200ms |
| **Total Latency** | 200-700ms |
| **Memory Usage** | ~50MB |

### Update Cycle

```
???????????????????????????????????????????
?  Sensor Monitoring Cycle (500ms)       ?
???????????????????????????????????????????
?                                         ?
?  1. Poll HWInfo64 shared memory        ?
?     ?? Read all sensor values          ?
?                                         ?
?  2. Process each configured dial       ?
?     ?? Get sensor reading               ?
?     ?? Calculate percentage (0-100)    ?
?     ?? Determine color (threshold)     ?
?     ?? Check if update needed          ?
?                                         ?
?  3. Update physical dials (if changed) ?
?     ?? Send position command           ?
?     ?? Send color command              ?
?                                         ?
?  4. Update UI (if window visible)      ?
?     ?? Refresh dial status displays    ?
?                                         ?
?  5. Wait for next cycle (500ms)        ?
?                                         ?
???????????????????????????????????????????
```

---

## Error Handling

### HWInfo64 Connection

The application retries HWInfo64 connection for up to 5 minutes:

```csharp
// VUWare.App\Services\AppInitializationService.cs
private async Task<bool> ConnectHWInfoAsync(CancellationToken cancellationToken)
{
    var hwInfoInit = new HWiNFOInitializationService(
        reader: hwInfoReader,
        retryIntervalMs: 1000,
        maxTimeoutMs: 300000
    );
    
    bool connected = hwInfoInit.InitializeSync(cancellationToken);
    
    if (!connected)
    {
        return false;  // Continue without sensor monitoring
    }
}
```

### Serial Port Communication

All serial operations include timeout protection:

```csharp
public async Task<string> SendCommandAsync(string command, int timeoutMs = 2000)
{
    try
    {
        await _asyncLock.WaitAsync(cancellationToken);
        string response = await ReadResponseAsync(timeoutMs, cancellationToken);
        return response;
    }
    catch (TimeoutException)
    {
        Debug.WriteLine($"Timeout after {timeoutMs}ms");
        throw;
    }
    catch (InvalidOperationException ex)
    {
        Debug.WriteLine($"Communication error: {ex.Message}");
        throw;
    }
}
```

---

## System Behavior Under Load

### HWInfo64 Shared Memory Architecture

VUWare reads sensor data from HWInfo64's shared memory, which is updated by a background thread within HWInfo64:

```
HWInfo64 Internal Architecture:
???????????????????????????????????????????????????????
Sensor Reader Thread (High Priority)
?? Reads hardware directly
?? Updates internal buffers
?? Drives HWInfo64 UI

Shared Memory Writer Thread (Normal Priority)
?? Copies from internal buffers to memory-mapped file
?? Updates at configured polling rate (default: 2000ms)
```

The application is designed to handle variations in shared memory update frequency gracefully through timeout protection and data caching.

### Load Scenarios

| CPU Load | Typical Scenario | Application Behavior |
|----------|------------------|---------------------|
| 0-80% | Normal operation, games, productivity apps | Normal operation (200-700ms latency) |
| 80-95% | Heavy multitasking, compilation, rendering | Normal operation (500-1000ms latency) |
| 95-100% | Sustained high load across most threads | Cached values displayed, updates when data available |

---

## Diagnostic Features

### Debug Logging

Enable diagnostic logging in configuration:

```json
{
  "appSettings": {
    "debugMode": true
  }
}
```

**Output (Visual Studio Debug window):**
```
[HWiNFOReader] ? SUCCESS - Gap since last: 502ms, This read took: 3ms, Got 150 readings
[HWiNFOReader] ? SUCCESS - Gap since last: 498ms, This read took: 2ms, Got 150 readings
```

### Performance Metrics

Each dial tracks update statistics:

```csharp
// VUWare.App\Services\SensorMonitoringService.cs
private class DialMonitoringState
{
    public int UpdateCount { get; set; }              // Total successful updates
    public DateTime LastUpdate { get; set; }          // Last successful update time
    public DateTime LastPhysicalUpdate { get; set; }  // Last dial movement time
}
```

---

## Project Structure

### VUWare.App (WPF Application)
- **MainWindow.xaml/cs** - Main UI
- **Services/**
  - `AppInitializationService` - Startup and connection management
  - `SensorMonitoringService` - Sensor polling and dial updates
  - `SystemTrayManager` - System tray integration
  - `ConfigManager` - Configuration file management
- **Models/** - Configuration and view models
- **Config/** - JSON configuration files

### VUWare.Lib (Core Library)
- **VU1Controller** - High-level dial control API
- **DeviceManager** - Device discovery and command routing
- **SerialPortManager** - USB/Serial communication with async I/O
- **CommandBuilder** - Serial protocol command generation
- **ProtocolHandler** - Response parsing and validation
- **ImageProcessor** - Display image format conversion

### VUWare.HWInfo64 (Sensor Library)
- **HWiNFOReader** - Low-level shared memory access
- **HWInfo64Controller** - High-level sensor API with polling
- **HWiNFOStructures** - Native structure definitions
- **SensorModels** - Managed data models

### VUWare.Console (Console Tool)
- Command-line interface for testing and diagnostics

---

## External Dependencies

### Hardware Requirements
- **VU1 Gauge Hub** - USB-connected hub (FTDI VID:0x0403 PID:0x6015)
- **VU1 Dials** - I2C-connected analog dials

### Software Requirements
- **.NET 8.0 Runtime** - Windows desktop runtime
- **HWInfo64** - Must be running with:
  - Sensors-only mode
  - Shared Memory Support enabled
  - Recommended polling period: 500ms

---

## Future Enhancement Options

### LibreHardwareMonitor Integration

For direct hardware access without HWInfo64 dependency:

```csharp
using LibreHardwareMonitor.Hardware;

var computer = new Computer
{
    IsCpuEnabled = true,
    IsGpuEnabled = true,
    IsMemoryEnabled = true
};
computer.Open();

foreach (var hardware in computer.Hardware)
{
    hardware.Update();
    foreach (var sensor in hardware.Sensors)
    {
        // Direct hardware access
        float value = sensor.Value ?? 0;
    }
}
```

**Benefits:**
- No shared memory dependency
- Direct hardware access
- Consistent latency under all load conditions

**Considerations:**
- Requires administrator privileges
- Different sensor ID mapping
- Additional development effort (~1-2 days)

---

## Summary

VUWare provides reliable real-time hardware monitoring with physical dial feedback through:

- **Efficient Architecture** - Dedicated threads for time-critical operations
- **Resilient Design** - Timeout protection and data caching
- **Async I/O** - Non-blocking serial communication
- **Flexible Configuration** - JSON-based sensor mapping
- **Production Ready** - Comprehensive error handling and logging

**Typical Performance:**
- 200-700ms total latency from sensor read to dial movement
- <1% CPU usage
- ~50MB memory footprint
- Reliable operation across varied system loads
