# VUWare.App - Initialization System

## Overview

The WPF application now includes a complete asynchronous initialization system that:
1. Loads dial configuration from JSON
2. Connects to VU1 Gauge Hub on a background thread
3. Discovers and initializes connected dials
4. Connects to HWInfo64 for sensor monitoring
5. Updates UI with status information without blocking the UI thread

## Architecture

### AppInitializationService
**File:** `VUWare.App/Services/AppInitializationService.cs`

Core service that manages all initialization steps on a dedicated background thread.

**Key Features:**
- Async/await based initialization pipeline
- Non-blocking UI updates using WPF Dispatcher
- Proper cancellation support with CancellationToken
- Graceful error handling with detailed error messages
- Event-based status reporting

**Initialization Stages:**
```
1. ConnectingDials     ? Detecting and connecting to VU1 Hub
2. InitializingDials   ? Discovering dials via I2C bus scan
3. ConnectingHWInfo    ? Connecting to HWInfo64 shared memory
4. Monitoring          ? Ready for sensor-to-dial updates
```

### Public Interface

```csharp
// Start initialization
service.StartInitialization();

// Subscribe to events
service.OnStatusChanged += (status) => { /* update UI */ };
service.OnError += (message) => { /* show error */ };
service.OnInitializationComplete += () => { /* enable features */ };

// Query state
bool initialized = service.IsInitialized;
bool initializing = service.IsInitializing;

// Get controllers after initialization
VU1Controller vu1 = service.GetVU1Controller();
HWInfo64Controller hwinfo = service.GetHWInfo64Controller();
```

## Initialization Flow

```
MainWindow.Loaded
    ?
LoadConfiguration()
    ?? Load dials-config.json
    ?? Validate configuration
    ?? Show errors if invalid
    ?
StartInitialization()
    ?? Create AppInitializationService
        ?? Service.StartInitialization() [Background Thread]
            ?? ConnectingDials status
            ?   ?? VU1Controller.AutoDetectAndConnect()
            ?       ?? Scan COM ports
            ?       ?? Open serial connection
            ?       ?? Verify hub is responding
            ?? InitializingDials status
            ?   ?? VU1Controller.InitializeAsync()
            ?       ?? Scan I2C bus
            ?       ?? Discover all dials
            ?       ?? Query dial information
            ?       ?? Set initial state (0%, normal color)
            ?? ConnectingHWInfo status
            ?   ?? HWInfo64Controller.Connect()
            ?       ?? Open shared memory connection
            ?       ?? Register sensor mappings from config
            ?       ?? Start periodic polling
            ?? Monitoring status
            ?   ?? Ready for sensor updates
            ?? OnInitializationComplete event
                ?? UI can now enable monitoring features
```

## UI Integration

### Status Button Updates

The Status Button displays initialization progress with color coding:

| Status | Text | Color | Meaning |
|--------|------|-------|---------|
| ConnectingDials | "Connecting Dials" | Yellow | In progress |
| InitializingDials | "Initializing Dials" | Yellow | In progress |
| ConnectingHWInfo | "Connecting HWInfo Sensors" | Yellow | In progress |
| Monitoring | "Monitoring" | Green | Ready |
| Failed | "Initialization Failed" | Red | Error occurred |

### Event Handling

```csharp
// Status updates
_initService.OnStatusChanged += (status) =>
{
    // Update button text and color
    StatusButton.Content = status.ToString();
};

// Error messages
_initService.OnError += (message) =>
{
    // Show message box to user
    MessageBox.Show(message);
};

// Completion
_initService.OnInitializationComplete += () =>
{
    // Enable monitoring features
    // Access controllers via _initService.GetVU1Controller()
};
```

## Configuration Integration

The initialization service uses the `DialsConfiguration` to:

1. **Initialize Dials**
   - Set each dial to 0% position
   - Set each dial to its configured normal color
   - Skip disabled dials

2. **Register HWInfo Sensors**
   - Create DialSensorMapping for each enabled dial
   - Use sensor name/entry name from config
   - Use min/max/threshold values from config
   - Register in HWInfo64Controller for polling

3. **Apply Settings**
   - Use global update interval from AppSettings
   - Use per-dial update intervals from DialConfig
   - Honor enabled/disabled flags

## Thread Safety

All operations are properly thread-safe:

### Background Thread (Initialization Worker)
- Runs all I/O operations (serial, shared memory)
- Performs blocking operations without UI impact
- Uses CancellationToken for graceful shutdown

### UI Thread
- All UI updates marshalled via `Dispatcher.Invoke()`
- Event handlers execute on UI thread
- Cannot block UI thread

### Controller Access
- VU1Controller and HWInfo64Controller are thread-safe
- Can be safely accessed from UI thread after initialization

## Error Handling

The service provides comprehensive error handling:

```
Connection Error
    ?
RaiseError("Failed to connect to VU1 dials...")
    ?
Dispatcher.Invoke() ? OnError event
    ?
MainWindow shows MessageBox
    ?
Status set to Failed
```

## Usage Example

```csharp
private AppInitializationService _initService;

private void MainWindow_Loaded(object sender, RoutedEventArgs e)
{
    // Load config first
    var config = ConfigManager.LoadDefault();
    
    // Create and start initialization
    _initService = new AppInitializationService(config);
    _initService.OnStatusChanged += (status) =>
    {
        StatusButton.Content = status.ToString();
    };
    _initService.OnError += (msg) =>
    {
        MessageBox.Show(msg);
    };
    _initService.OnInitializationComplete += () =>
    {
        // Now ready to use
        var vu1 = _initService.GetVU1Controller();
        var hwinfo = _initService.GetHWInfo64Controller();
        StartMonitoring(vu1, hwinfo);
    };
    
    // Start background initialization
    _initService.StartInitialization();
}

private void StartMonitoring(VU1Controller vu1, HWInfo64Controller hwinfo)
{
    // Implementation in next phase...
}

private void MainWindow_Closing(object sender, CancelEventArgs e)
{
    _initService?.Dispose();
}
```

## Configuration File Reference

Example configuration for the initialization service:

```json
{
  "version": "1.0",
  "appSettings": {
    "autoConnect": true,
    "enablePolling": true,
    "globalUpdateIntervalMs": 1000,
    "logFilePath": "",
    "debugMode": false
  },
  "dials": [
    {
      "dialUid": "290063000750524834313020",
      "displayName": "CPU Usage",
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
      },
      "enabled": true,
      "updateIntervalMs": 500
    }
  ]
}
```

## Next Steps

Once initialization completes, the monitoring phase will:
1. Poll HWInfo64 sensors at configured intervals
2. Read current sensor values
3. Map sensor values to dial percentages using min/max ranges
4. Apply threshold-based color changes
5. Update dials via serial protocol
6. Handle real-time sensor monitoring

See `MONITORING_IMPLEMENTATION.md` for details on the monitoring phase.

## Troubleshooting

### Initialization Stuck
- Check if HWInfo64 is running (Connecting HWInfo step)
- Check if VU1 Hub is powered and connected via USB
- Check Device Manager for COM port assignment

### Dials Not Discovered
- Verify I2C cables between hub and dials
- Power cycle the entire system
- Check USB cable quality

### HWInfo Connection Failed
- Ensure HWInfo64 is running in "Sensors only" mode
- Enable "Shared Memory Support" in HWInfo64 Options
- Restart HWInfo64

### Configuration Errors
- Validate JSON syntax in dials-config.json
- Check sensor names match exactly (case-sensitive in some cases)
- Verify all required fields are present
