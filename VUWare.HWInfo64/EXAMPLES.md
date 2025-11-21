# HWInfo64 + VU1 Integration Examples

This guide shows practical examples of integrating HWInfo64 sensor readings with VU1 dials.

## Example 1: Simple CPU Temperature Monitor

Monitor CPU temperature on a single dial:

```csharp
using VUWare.Lib;
using VUWare.HWInfo64;
using System.Threading.Tasks;

class CPUTempMonitor
{
    private VU1Controller? _vuController;
    private HWInfo64Controller? _hwController;
    private string _dialUID = "YOUR_DIAL_UID";

    public async Task RunAsync()
    {
        try
        {
            // Initialize VU1
            _vuController = new VU1Controller();
            if (!_vuController.AutoDetectAndConnect())
            {
                Console.WriteLine("Failed to connect to VU1");
                return;
            }

            if (!await _vuController.InitializeAsync())
            {
                Console.WriteLine("Failed to initialize dials");
                return;
            }

            // Initialize HWInfo64
            _hwController = new HWInfo64Controller();
            
            var mapping = new DialSensorMapping
            {
                Id = "cpu-temp",
                SensorName = "CPU Package",
                EntryName = "Temperature",
                MinValue = 20,
                MaxValue = 100,
                WarningThreshold = 80,
                CriticalThreshold = 95,
                DisplayName = "CPU Temperature"
            };
            
            _hwController.RegisterDialMapping(mapping);

            if (!_hwController.Connect())
            {
                Console.WriteLine("Failed to connect to HWInfo64");
                return;
            }

            _hwController.OnSensorValueChanged += UpdateDial;

            Console.WriteLine("CPU Temperature Monitor Running");
            Console.WriteLine("Press Ctrl+C to exit");
            
            while (true)
            {
                await Task.Delay(100);
            }
        }
        finally
        {
            _hwController?.Disconnect();
            _vuController?.Dispose();
        }
    }

    private async void UpdateDial(string mappingId, SensorReading reading)
    {
        if (mappingId != "cpu-temp") return;

        var status = _hwController?.GetSensorStatus(mappingId);
        if (status == null) return;

        // Update dial position
        await _vuController.SetDialPercentageAsync(_dialUID, status.Percentage);

        // Update color based on temperature
        var (r, g, b) = status.GetRecommendedColor();
        await _vuController.SetBacklightAsync(_dialUID, r, g, b);

        Console.WriteLine($"CPU Temp: {reading.Value:F1}°C - Dial: {status.Percentage}% - " +
            (status.IsCritical ? "CRITICAL!" : status.IsWarning ? "WARNING" : "OK"));
    }
}
```

## Example 2: Multi-Sensor Dashboard

Monitor multiple sensors on different dials:

```csharp
class MultiSensorDashboard
{
    private VU1Controller? _vuController;
    private HWInfo64Controller? _hwController;
    private Dictionary<string, string> _mappingToDial;

    public async Task RunAsync()
    {
        _mappingToDial = new Dictionary<string, string>
        {
            { "cpu-temp", "DIAL_UID_1" },
            { "gpu-temp", "DIAL_UID_2" },
            { "cpu-load", "DIAL_UID_3" },
            { "gpu-load", "DIAL_UID_4" }
        };

        // Initialize controllers...
        _vuController = new VU1Controller();
        _hwController = new HWInfo64Controller();

        // Register all mappings
        RegisterMappings();

        if (!_hwController.Connect())
        {
            Console.WriteLine("Failed to connect to HWInfo64");
            return;
        }

        _hwController.OnSensorValueChanged += UpdateAllDials;
        
        Console.WriteLine("Dashboard running. Press Enter to exit...");
        Console.ReadLine();

        _hwController.Disconnect();
    }

    private void RegisterMappings()
    {
        _hwController.RegisterDialMapping(new DialSensorMapping
        {
            Id = "cpu-temp",
            SensorName = "CPU Package",
            EntryName = "Temperature",
            MinValue = 20,
            MaxValue = 100,
            WarningThreshold = 85,
            CriticalThreshold = 95
        });

        _hwController.RegisterDialMapping(new DialSensorMapping
        {
            Id = "gpu-temp",
            SensorName = "GPU",
            EntryName = "Temperature",
            MinValue = 20,
            MaxValue = 90,
            WarningThreshold = 80,
            CriticalThreshold = 85
        });

        _hwController.RegisterDialMapping(new DialSensorMapping
        {
            Id = "cpu-load",
            SensorName = "CPU Package",
            EntryName = "Load",
            MinValue = 0,
            MaxValue = 100,
            WarningThreshold = 80,
            CriticalThreshold = 95
        });

        _hwController.RegisterDialMapping(new DialSensorMapping
        {
            Id = "gpu-load",
            SensorName = "GPU",
            EntryName = "Load",
            MinValue = 0,
            MaxValue = 100,
            WarningThreshold = 80,
            CriticalThreshold = 95
        });
    }

    private async void UpdateAllDials(string mappingId, SensorReading reading)
    {
        if (!_mappingToDial.TryGetValue(mappingId, out var dialUID))
            return;

        var status = _hwController.GetSensorStatus(mappingId);
        if (status == null) return;

        try
        {
            await _vuController.SetDialPercentageAsync(dialUID, status.Percentage);
            
            var (r, g, b) = status.GetRecommendedColor();
            await _vuController.SetBacklightAsync(dialUID, r, g, b);

            Console.WriteLine($"[{mappingId}] {reading.Value:F1} {reading.Unit} " +
                $"({status.Percentage}%) {(status.IsCritical ? "??" : status.IsWarning ? "??" : "??")}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating {mappingId}: {ex.Message}");
        }
    }
}
```

## Example 3: Sensor Discovery

Find all available sensors on your system:

```csharp
class SensorDiscovery
{
    public void PrintAllSensors()
    {
        var reader = new HWiNFOReader();
        
        if (!reader.Connect())
        {
            Console.WriteLine("Failed to connect to HWInfo64");
            Console.WriteLine("Make sure HWInfo64 is running with Shared Memory Support enabled");
            return;
        }

        var readings = reader.ReadAllSensorReadings();

        // Group by sensor name
        var grouped = readings
            .GroupBy(r => r.SensorName)
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            Console.WriteLine($"\n[{group.Key}]");
            foreach (var reading in group.OrderBy(r => r.EntryName))
            {
                Console.WriteLine($"  ?? {reading.EntryName}");
                Console.WriteLine($"  ?  Type: {reading.Type}");
                Console.WriteLine($"  ?  Value: {reading.Value:F2} {reading.Unit}");
                Console.WriteLine($"  ?  Range: {reading.ValueMin:F2} - {reading.ValueMax:F2}");
                Console.WriteLine($"  ?  Entry Name (for mapping): \"{reading.EntryName}\"");
                Console.WriteLine($"  ?  Sensor Name (for mapping): \"{reading.SensorName}\"");
            }
        }

        reader.Disconnect();
    }
}
```

## Example 4: Advanced Threshold Management

Configure different thresholds based on hardware:

```csharp
class ThresholdManager
{
    public static Dictionary<string, DialSensorMapping> CreateOptimalMappings(string hardwareProfile)
    {
        var mappings = new Dictionary<string, DialSensorMapping>();

        switch (hardwareProfile)
        {
            case "Intel i9":
                mappings["cpu"] = new DialSensorMapping
                {
                    Id = "cpu",
                    SensorName = "CPU Package",
                    EntryName = "Temperature",
                    MinValue = 20,
                    MaxValue = 105,         // Higher limit for i9
                    WarningThreshold = 90,
                    CriticalThreshold = 100
                };
                break;

            case "AMD Ryzen":
                mappings["cpu"] = new DialSensorMapping
                {
                    Id = "cpu",
                    SensorName = "Core #0-0",
                    EntryName = "Temperature",
                    MinValue = 20,
                    MaxValue = 95,
                    WarningThreshold = 85,
                    CriticalThreshold = 90
                };
                break;

            case "RTX 4090":
                mappings["gpu"] = new DialSensorMapping
                {
                    Id = "gpu",
                    SensorName = "GPU",
                    EntryName = "Temperature",
                    MinValue = 20,
                    MaxValue = 90,
                    WarningThreshold = 80,
                    CriticalThreshold = 87
                };
                break;
        }

        return mappings;
    }
}
```

## Example 5: Error Handling and Resilience

Robust error handling for production use:

```csharp
class RobustSensorMonitor
{
    private HWInfo64Controller? _hwController;
    private int _reconnectAttempts = 0;
    private const int MAX_RECONNECT_ATTEMPTS = 5;
    private const int RECONNECT_DELAY_MS = 5000;

    public async Task RunAsync()
    {
        while (true)
        {
            try
            {
                _hwController = new HWInfo64Controller();
                
                if (await ConnectWithRetryAsync())
                {
                    RegisterMappings();
                    MonitorSensors();
                    _reconnectAttempts = 0;
                }
                else
                {
                    throw new Exception("Failed to connect to HWInfo64");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                
                _reconnectAttempts++;
                if (_reconnectAttempts >= MAX_RECONNECT_ATTEMPTS)
                {
                    Console.WriteLine("Max reconnect attempts reached. Exiting.");
                    break;
                }

                Console.WriteLine($"Reconnecting in {RECONNECT_DELAY_MS}ms... (Attempt {_reconnectAttempts})");
                await Task.Delay(RECONNECT_DELAY_MS);
            }
            finally
            {
                _hwController?.Disconnect();
                _hwController?.Dispose();
            }
        }
    }

    private async Task<bool> ConnectWithRetryAsync()
    {
        for (int i = 0; i < 3; i++)
        {
            if (_hwController.Connect())
                return true;
            
            await Task.Delay(1000);
        }
        
        return false;
    }

    private void RegisterMappings()
    {
        _hwController.RegisterDialMapping(new DialSensorMapping
        {
            Id = "cpu-temp",
            SensorName = "CPU Package",
            EntryName = "Temperature",
            MinValue = 20,
            MaxValue = 100,
            WarningThreshold = 80,
            CriticalThreshold = 95
        });
    }

    private void MonitorSensors()
    {
        _hwController.OnSensorValueChanged += (id, reading) =>
        {
            try
            {
                Console.WriteLine($"[{reading.LastUpdate:HH:mm:ss}] {reading.SensorName}: {reading.Value:F1} {reading.Unit}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing sensor update: {ex.Message}");
            }
        };

        // Keep monitoring
        while (_hwController.IsConnected)
        {
            Task.Delay(1000).Wait();
        }
    }
}
```

## Example 6: Data Logging

Log sensor data for analysis:

```csharp
class SensorDataLogger
{
    private StreamWriter? _logFile;
    private HWInfo64Controller? _hwController;

    public async Task RunAsync(string logFilePath)
    {
        try
        {
            _logFile = new StreamWriter(logFilePath, append: true)
            {
                AutoFlush = true
            };

            _hwController = new HWInfo64Controller();
            
            _hwController.RegisterDialMapping(new DialSensorMapping
            {
                Id = "cpu-temp",
                SensorName = "CPU Package",
                EntryName = "Temperature",
                MinValue = 20,
                MaxValue = 100
            });

            if (!_hwController.Connect())
            {
                Console.WriteLine("Failed to connect to HWInfo64");
                return;
            }

            _logFile.WriteLine($"=== Sensor Log Started: {DateTime.Now:G} ===");

            _hwController.OnSensorValueChanged += LogSensorData;

            Console.WriteLine($"Logging to {logFilePath}. Press Ctrl+C to stop.");
            while (true)
            {
                await Task.Delay(1000);
            }
        }
        finally
        {
            _hwController?.Disconnect();
            _logFile?.WriteLine($"=== Sensor Log Ended: {DateTime.Now:G} ===");
            _logFile?.Dispose();
        }
    }

    private void LogSensorData(string mappingId, SensorReading reading)
    {
        _logFile?.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}," +
            $"{reading.SensorName}," +
            $"{reading.EntryName}," +
            $"{reading.Value:F2}," +
            $"{reading.Unit}");
    }
}
```

## Testing Your Integration

Before deploying to production, test with:

```csharp
class IntegrationTest
{
    public async Task TestHWInfoConnectionAsync()
    {
        Console.WriteLine("Testing HWInfo64 connection...");
        
        var reader = new HWiNFOReader();
        if (!reader.Connect())
        {
            Console.WriteLine("? Failed to connect to HWInfo64");
            Console.WriteLine("   Make sure:");
            Console.WriteLine("   1. HWInfo64 is running");
            Console.WriteLine("   2. Running in 'Sensors only' mode");
            Console.WriteLine("   3. 'Shared Memory Support' is enabled in Options");
            return;
        }

        Console.WriteLine("? Connected to HWInfo64");

        var readings = reader.ReadAllSensorReadings();
        Console.WriteLine($"? Found {readings.Count} sensor readings");
        
        reader.Disconnect();
    }

    public async Task TestDialMappingAsync(string sensorName, string entryName)
    {
        var controller = new HWInfo64Controller();
        
        var mapping = new DialSensorMapping
        {
            Id = "test",
            SensorName = sensorName,
            EntryName = entryName,
            MinValue = 0,
            MaxValue = 100
        };

        controller.RegisterDialMapping(mapping);

        if (!controller.Connect())
        {
            Console.WriteLine("? Failed to connect");
            return;
        }

        controller.OnSensorValueChanged += (id, reading) =>
        {
            var status = controller.GetSensorStatus(id);
            Console.WriteLine($"? {reading.SensorName}: {reading.Value:F2} {reading.Unit}");
            Console.WriteLine($"  Dial: {status.Percentage}%");
        };

        Console.WriteLine($"Testing mapping for '{sensorName}' > '{entryName}'");
        await Task.Delay(3000);

        controller.Disconnect();
    }
}
```

## Deployment Checklist

- [ ] HWInfo64 can be started automatically on system boot
- [ ] Shared Memory Support is enabled in HWInfo64 settings
- [ ] Reconnection logic handles HWInfo64 restarts
- [ ] Error handling logs issues appropriately
- [ ] Dial UIDs are correct and tested
- [ ] Thresholds are appropriate for your hardware
- [ ] Polling interval is suitable (500ms recommended)
- [ ] Application gracefully handles VU1 disconnection
