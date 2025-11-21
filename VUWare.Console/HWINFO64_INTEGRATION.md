# VUWare Console - HWInfo64 Integration Guide

## New Commands Added

The VUWare Console now includes two new commands for integrating HWInfo64 sensor data with VU1 dials:

### 1. `sensors` - List All Available Sensors

Lists all sensors currently available in HWInfo64 shared memory.

**Usage:**
```
sensors
```

**Example Output:**
```
?? HWInfo64 Sensors ?????????????????????????????????????????????
? [CPU Package]
?   ?? Temperature                                   ?
?   ?  Value: 45.50 °C        Min: 28.00 Max: 87.50 ?
?   ?? Clock                                          ?
?   ?  Value: 2400.00 MHz      Min: 800.00 Max: 5000 ?
?   ?? Power                                          ?
?   ?  Value: 65.20 W          Min: 5.00 Max: 150.00 ?
?
? [GPU]
?   ?? Temperature                                   ?
?   ?  Value: 38.00 °C        Min: 20.00 Max: 90.00 ?
?   ?? Load                                           ?
?   ?  Value: 12.50 %         Min: 0.00 Max: 100.00 ?
?
? Total: 5 sensor reading(s)                           ?
??????????????????????????????????????????????????????????????????
```

**Requirements:**
- HWInfo64 must be running in "Sensors only" mode
- "Shared Memory Support" must be enabled in HWInfo64 Options

**Troubleshooting:**
If you see "HWInfo64 not available", check:
1. HWInfo64 is running
2. Running in "Sensors only" mode (not Summary)
3. Options ? Enable "Shared Memory Support"

---

### 2. `monitor` - Monitor Sensor Data on a Dial

Continuously polls a specific HWInfo64 sensor and updates a dial with the value mapped to 0-100%.

**Usage:**
```
monitor <dial_uid> <sensor_name:entry_name> [poll_interval_ms]
```

**Parameters:**
- `<dial_uid>` - The unique ID of the target dial (from `dials` command)
- `<sensor_name:entry_name>` - The sensor and entry to monitor (use `sensors` to find)
- `[poll_interval_ms]` - Optional polling interval in milliseconds (default: 500)

**Example:**
```
monitor ABC123 "CPU Package:Temperature" 500
```

**Output:**
```
?? Sensor Monitor ????????????????????????????????????????????????
? Dial: CPU Temperature Monitor                                  ?
? Sensor: CPU Package                                            ?
? Entry: Temperature                                             ?
? Poll Interval: 500ms                                           ?
?                                                               ?
? Press any key to stop monitoring...                           ?
?????????????????????????????????????????????????????????????????

[12:34:56] Value: 45.3 °C    ? Dial: 45% ? Updates: 1
[12:34:57] Value: 46.1 °C    ? Dial: 46% ? Updates: 2
[12:34:58] Value: 46.8 °C    ? Dial: 47% ? Updates: 3
```

**Auto-Scaling by Sensor Type:**

The monitor command automatically determines appropriate scaling based on sensor type:

| Sensor Type | Scale Range | Mapping |
|---|---|---|
| Temperature | 0-100°C | percentage = (value / 100) × 100 |
| Usage/Load | 0-100% | percentage = clamp(value, 0, 100) |
| Voltage | 0-5V | percentage = (value / 5) × 100 |
| Fan (RPM) | 0-5000 RPM | percentage = (value / 5000) × 100 |
| Power (Watts) | 0-500W | percentage = (value / 500) × 100 |
| Other | Dynamic | Uses min/max from sensor data |

**Exit:**
- Press any key to stop monitoring
- Dial will automatically reset to 0% and green backlight

**Features:**
- ? Real-time sensor updates
- ? Automatic percentage scaling
- ? Configurable polling interval
- ? Backlight color indicators (optional, can be added)
- ? Sensor change logging

---

## Complete Workflow Example

### Step 1: Connect and Initialize
```
> connect
? Connected to VU1 Hub!

> init
? Initialized! Found 2 dial(s).

> dials
?? Dials ?????????????????????????????????????????????????????????
? [1] CPU Temperature Monitor                                    ?
?   UID:  ABC123...                                              ?
?   Pos:    0% ? Light: RGB(  0,  0,  0)                         ?
?                                                               ?
? [2] GPU Temperature Monitor                                    ?
?   UID:  XYZ789...                                              ?
?   Pos:    0% ? Light: RGB(  0,  0,  0)                         ?
??????????????????????????????????????????????????????????????????
```

### Step 2: Discover Available Sensors
```
> sensors
?? HWInfo64 Sensors ?????????????????????????????????????????????
? [CPU Package]
?   ?? Temperature
?   ?  Value: 45.50 °C        Min: 28.00 Max: 87.50 ?
...
??????????????????????????????????????????????????????????????????
```

### Step 3: Monitor CPU Temperature on First Dial
```
> monitor ABC123 "CPU Package:Temperature" 500

?? Sensor Monitor ????????????????????????????????????????????????
? Dial: CPU Temperature Monitor                                  ?
? Sensor: CPU Package                                            ?
? Entry: Temperature                                             ?
? Poll Interval: 500ms                                           ?
?                                                               ?
? Press any key to stop monitoring...                           ?
?????????????????????????????????????????????????????????????????

[12:34:56] Value: 45.3 °C    ? Dial: 45% ? Updates: 1
[12:34:57] Value: 46.1 °C    ? Dial: 46% ? Updates: 2
...
(Press any key)

? Monitoring ended. Sent 25 updates to dial.
```

### Step 4: Monitor GPU Load on Second Dial (in another session)
```
> monitor XYZ789 "GPU:Load" 500

?? Sensor Monitor ????????????????????????????????????????????????
? Dial: GPU Temperature Monitor                                  ?
? Sensor: GPU                                                    ?
? Entry: Load                                                    ?
? Poll Interval: 500ms                                           ?
?                                                               ?
? Press any key to stop monitoring...                           ?
?????????????????????????????????????????????????????????????????

[12:34:56] Value: 12.5 %     ? Dial: 12% ? Updates: 1
[12:34:57] Value: 15.2 %     ? Dial: 15% ? Updates: 2
...
```

---

## Sensor Naming Guide

### Common Sensor Names

**CPU:**
- "CPU Package" - Overall CPU temperature
- "CPU Core #0", "CPU Core #1" - Individual core temps
- "Package Power" - Total CPU power draw

**GPU (NVIDIA):**
- "GPU" - GPU temperature and load
- "GPU Memory" - VRAM temperature

**GPU (AMD):**
- "GPU Core" - Core temperature
- "HBM" - High Bandwidth Memory temperature

**Motherboard:**
- "Motherboard" - Motherboard temperature
- "System" - System temperature

**Fans:**
- "Fan #1", "Fan #2" - Fan speeds (RPM)

**Common Entry Names:**
- "Temperature" - Current temperature
- "Load" / "Usage" - Current utilization percentage
- "Power" - Power draw in Watts
- "Clock" - Current frequency in MHz
- "Speed" - Fan speed in RPM

### Finding Exact Sensor Names

Use the `sensors` command to see the exact names available on your system:

```
> sensors
```

Look for the format:
```
[Sensor Name]
  ?? Entry Name
  ?  Value: X.XX Unit
```

---

## Polling Interval Recommendations

| Use Case | Interval | Notes |
|---|---|---|
| Temperature monitoring | 500-1000ms | Good responsiveness with low overhead |
| Fan speed | 1000-2000ms | Changes less frequently |
| CPU/GPU load | 250-500ms | More responsive for dynamic loads |
| Power draw | 500-1000ms | Smooth without excessive updates |
| Detailed monitoring | 100-250ms | May use more CPU, better resolution |

**Default:** 500ms (good general-purpose value)

---

## Troubleshooting

### "HWInfo64 not available" when running `sensors`

**Solution:**
1. Launch HWInfo64
2. Select "Sensors only" mode (not Summary)
3. Go to Options ? Check "Shared Memory Support"
4. Keep HWInfo64 running
5. Try `sensors` again

### "Sensor not found" when running `monitor`

**Causes:**
- Sensor/entry name doesn't match exactly
- Sensor not available on your hardware
- HWInfo64 not running

**Solution:**
1. Run `sensors` to see exact available names
2. Copy the exact sensor and entry names (case-insensitive match, but spelling must match)
3. Use format: `monitor <uid> "Sensor:Entry"`

**Example:**
```
> monitor ABC123 "CPU Package:Temperature"
              ? Note: Space after colon is important, or no space, match exactly
```

### Dial not updating

**Check:**
1. Dial UID is correct (`dials` command shows correct UID)
2. VU1 is connected and initialized
3. Sensor name matches exactly (use `sensors` to verify)
4. HWInfo64 is running in Sensors mode

### Updates are too frequent or too slow

Adjust the polling interval:
```
monitor ABC123 "CPU Package:Temperature" 1000  # Slower (1 second)
monitor ABC123 "CPU Package:Temperature" 250   # Faster (250ms)
```

---

## Integration Tips

### Multi-Sensor Setup

Monitor multiple sensors on different dials by running multiple console instances:
1. Console 1: `monitor ABC123 "CPU Package:Temperature"`
2. Console 2: `monitor XYZ789 "GPU:Temperature"`
3. Console 3: `monitor DEF456 "GPU:Load"`

### Custom Scaling

If default scaling doesn't work for your sensors, you can:
1. Run `sensors` to see min/max values
2. Calculate custom range: `max_value - min_value`
3. The monitor command will auto-scale based on min/max

Example for a sensor with 10-90 range:
- Sensor reports 10 ? Dial shows 0%
- Sensor reports 50 ? Dial shows 50%
- Sensor reports 90 ? Dial shows 100%

---

## Help Command

For quick reference in the console:
```
> help
```

Shows all available commands including the new `sensors` and `monitor` commands.

---

## See Also

- VUWare.HWInfo64 library documentation
- HWInfo64 official website: https://www.hwinfo.com/
- Quick Reference: `VUWare.HWInfo64\QUICK_REFERENCE.md`
