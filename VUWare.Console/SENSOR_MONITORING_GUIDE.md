# VUWare Console - HWInfo64 Integration Summary

## Overview

The VUWare Console has been enhanced with two new commands for integrating HWInfo64 sensor monitoring with VU1 dials:

- **`sensors`** - Discover and list all available HWInfo64 sensors
- **`monitor`** - Real-time sensor monitoring displayed on a VU1 dial

---

## Command 1: `sensors`

### Purpose
Lists all sensors and readings currently available from HWInfo64 shared memory.

### Usage
```
sensors
```

### Output Example
```
?? HWInfo64 Sensors ?????????????????????????????????????????????
? [CPU Package]
?   ?? Temperature
?   ?  Value: 45.50 °C        Min: 28.00 Max: 87.50
?   ?? Clock
?   ?  Value: 2400.00 MHz     Min: 800.00 Max: 5000.00
?   ?? Power
?   ?  Value: 65.20 W         Min: 5.00 Max: 150.00
?
? [GPU]
?   ?? Temperature
?   ?  Value: 38.00 °C        Min: 20.00 Max: 90.00
?   ?? Load
?   ?  Value: 12.50 %         Min: 0.00 Max: 100.00
?
? Total: 5 sensor reading(s)                           ?
??????????????????????????????????????????????????????????????????
```

### Requirements
1. HWInfo64 must be running
2. Must be in "Sensors only" mode (not Summary mode)
3. "Shared Memory Support" must be enabled in HWInfo64 Options

### Troubleshooting
**Error: "HWInfo64 not available"**
- Check HWInfo64 is running
- Go to Options ? Enable "Shared Memory Support"
- Make sure it's in "Sensors only" mode

---

## Command 2: `monitor`

### Purpose
Continuously polls a specific HWInfo64 sensor and updates a VU1 dial with the value in real-time until a key is pressed.

### Usage
```
monitor <dial_uid> <sensor_name:entry_name> [poll_interval_ms]
```

### Parameters
| Parameter | Description | Example |
|---|---|---|
| `<dial_uid>` | The UID of the target dial (from `dials` command) | `ABC123` |
| `<sensor_name:entry_name>` | Sensor and entry name (use exact names from `sensors` command) | `"CPU Package:Temperature"` |
| `[poll_interval_ms]` | Optional polling interval (default: 500ms) | `500` |

### Examples

#### Simple temperature monitoring
```
monitor ABC123 "CPU Package:Temperature"
```

#### CPU load on a different dial
```
monitor XYZ789 "CPU Package:Load" 1000
```

#### GPU temperature with 250ms updates
```
monitor DEF456 "GPU:Temperature" 250
```

### Interactive Output
```
?? Sensor Monitor ????????????????????????????????????????????????
? Dial: CPU Temperature Monitor                                  ?
? Sensor: CPU Package                                            ?
? Entry: Temperature                                             ?
? Poll Interval: 500ms                                           ?
?                                                               ?
? Press any key to stop monitoring...                           ?
?????????????????????????????????????????????????????????????????

[12:34:56] Value: 45.3 °C     ? Dial: 45% ? Updates: 1
[12:34:57] Value: 46.1 °C     ? Dial: 46% ? Updates: 2
[12:34:58] Value: 46.8 °C     ? Dial: 47% ? Updates: 3
[12:34:59] Value: 47.5 °C     ? Dial: 48% ? Updates: 4

? Monitoring ended. Sent 4 updates to dial.
```

### Features

? **Auto-Scaling by Sensor Type**
- Temperature: 0-100°C ? 0-100% dial
- Usage/Load: Already 0-100%
- Voltage: 0-5V ? 0-100% dial
- Fan: 0-5000 RPM ? 0-100% dial
- Power: 0-500W ? 0-100% dial
- Other: Uses sensor's min/max values

? **Real-Time Updates**
- Configurable polling interval (100ms - unlimited)
- Only logs significant changes (>0.5 units)
- Smooth dial transitions with easing

? **Clean Exit**
- Press any key to stop monitoring
- Dial automatically resets to 0%
- Backlight set to green

? **Error Handling**
- Detects missing sensors and shows available options
- Validates dial UID exists
- Graceful handling of HWInfo64 disconnection

---

## Step-by-Step Workflow

### 1. Start and Connect
```
> connect
? Connected to VU1 Hub!

> init
? Initialized! Found 2 dial(s).
```

### 2. List Your Dials
```
> dials
?? Dials ?????????????????????????????????????????????????????????
? [1] CPU Temperature Monitor
?   UID:  ABC123
?   Pos:    0% ? Light: RGB(  0,  0,  0)
?
? [2] GPU Load Monitor
?   UID:  XYZ789
?   Pos:    0% ? Light: RGB(  0,  0,  0)
??????????????????????????????????????????????????????????????????
```

### 3. Discover Available Sensors
```
> sensors
?? HWInfo64 Sensors ?????????????????????????????????????????????
? [CPU Package]
?   ?? Temperature
?   ?  Value: 45.50 °C        Min: 28.00 Max: 87.50
?   ?? Load
?   ?  Value: 12.30 %         Min: 0.00 Max: 100.00
...
```

### 4. Start Monitoring
```
> monitor ABC123 "CPU Package:Temperature" 500

?? Sensor Monitor ????????????????????????????????????????????????
? Dial: CPU Temperature Monitor
? Sensor: CPU Package
? Entry: Temperature
? Poll Interval: 500ms
?
? Press any key to stop monitoring...
?????????????????????????????????????????????????????????????????

[12:34:56] Value: 45.3 °C     ? Dial: 45% ? Updates: 1
[12:34:57] Value: 46.1 °C     ? Dial: 46% ? Updates: 2
...
(Press any key)

? Monitoring ended. Sent 10 updates to dial.
```

---

## Common Sensor Names by Hardware

### Intel CPU
- **Sensor:** `CPU Package`
- **Entries:** `Temperature`, `Load`, `Power`, `Clock`

### AMD CPU
- **Sensor:** `CPU Core` or `Core #X-Y`
- **Entries:** `Temperature`, `Load`

### NVIDIA GPU
- **Sensor:** `GPU`
- **Entries:** `Temperature`, `Load`, `Clock`, `Memory Clock`

### AMD GPU
- **Sensor:** `GPU Core`
- **Entries:** `Temperature`, `Load`

### Motherboard
- **Sensor:** `Motherboard` or `System`
- **Entries:** `Temperature`

### Fans
- **Sensor:** `Fan #1`, `Fan #2`, etc.
- **Entries:** `Speed`

**Use `sensors` command to see exact names on your system!**

---

## Sensor Name Format

The exact format for the monitor command is:
```
"Sensor Name:Entry Name"
```

**Important:**
- Use exact names from `sensors` output
- Names are case-insensitive for matching
- Include quotes if names have spaces
- Format is always `SensorName:EntryName` with colon separator

### Examples
```
monitor ABC123 "CPU Package:Temperature"
monitor ABC123 "CPU Package:Load"
monitor XYZ789 "GPU:Temperature"
monitor XYZ789 "GPU:Load"
monitor DEF456 "Fan #1:Speed"
```

---

## Polling Interval Guide

| Use Case | Recommended Interval | Reason |
|---|---|---|
| Temperature | 500-1000ms | Good balance of responsiveness and overhead |
| Fan Speed | 1000-2000ms | Changes less frequently, no need for updates |
| CPU/GPU Load | 250-500ms | More dynamic, benefits from faster updates |
| Power Draw | 500-1000ms | Moderate changes, smooth visual updates |
| Detailed Analysis | 100-250ms | Higher CPU overhead, better resolution |
| Slow Monitoring | 2000-5000ms | Minimal updates, good for debugging |

**Default:** 500ms (good general-purpose value)

---

## Auto-Scaling Examples

### Temperature Sensor
```
Value:  0°C  ? Dial:   0%
Value: 50°C  ? Dial:  50%
Value:100°C  ? Dial: 100%
```

### Load/Usage Sensor
```
Value:   0%  ? Dial:   0%
Value:  50%  ? Dial:  50%
Value: 100%  ? Dial: 100%
```

### Voltage Sensor (0-5V range)
```
Value: 0V    ? Dial:   0%
Value: 2.5V  ? Dial:  50%
Value: 5V    ? Dial: 100%
```

### Fan Speed (0-5000 RPM range)
```
Value:    0 RPM  ? Dial:   0%
Value: 2500 RPM  ? Dial:  50%
Value: 5000 RPM  ? Dial: 100%
```

### Power (0-500W range)
```
Value:   0W   ? Dial:   0%
Value: 250W   ? Dial:  50%
Value: 500W   ? Dial: 100%
```

---

## Troubleshooting

### "Dial not found"
- Run `dials` command to see valid UIDs
- Check you're copying the UID correctly
- Make sure VU1 is initialized (`init` command)

### "Sensor not found"
- Run `sensors` to see available sensors
- Check spelling of sensor name (must match exactly)
- Use exact names from `sensors` output
- Format: `"Sensor Name:Entry Name"`

### "HWInfo64 not available"
1. Ensure HWInfo64 is running
2. Check it's in "Sensors only" mode
3. Go to Options ? Enable "Shared Memory Support"
4. Restart HWInfo64 if changes made

### Dial not updating
- Check VU1 is connected and initialized
- Verify sensor name/entry name match exactly
- Check poll interval isn't too long
- Try running `sensors` again to confirm sensor exists

### Updates are slow
- Reduce poll interval: `monitor ABC123 "CPU Package:Temperature" 250`
- Minimum practical interval is around 100ms

### Updates are too frequent
- Increase poll interval: `monitor ABC123 "CPU Package:Temperature" 2000`
- Higher intervals reduce CPU usage

---

## Advanced Usage

### Monitoring Multiple Sensors Simultaneously
Open multiple console instances:
```
Console 1: monitor ABC123 "CPU Package:Temperature" 500
Console 2: monitor XYZ789 "GPU:Temperature" 500
Console 3: monitor DEF456 "CPU Package:Load" 500
```

### Creating a Sensor Monitoring Profile
Note the sensors you want to monitor:
```
> sensors
(Record sensor names you need)

> monitor DIAL1 "CPU Package:Temperature" 500
> monitor DIAL2 "GPU:Temperature" 500
> monitor DIAL3 "CPU Package:Load" 500
```

### Fine-Tuning Auto-Scaling
The monitor command includes type-based auto-scaling. If a sensor doesn't match the known types, it uses the sensor's min/max values from HWInfo64.

---

## Requirements Checklist

Before using the monitor command:
- [ ] HWInfo64 is installed and running
- [ ] HWInfo64 is in "Sensors only" mode
- [ ] "Shared Memory Support" is enabled in HWInfo64 Options
- [ ] VU1 Gauge Hub is connected
- [ ] VU1 is initialized (`init` command)
- [ ] Dials are discovered (`dials` command shows your dials)
- [ ] You know the dial UID (from `dials` command)
- [ ] You know the sensor name and entry (from `sensors` command)

---

## Related Commands

- `connect` - Connect to VU1 hub
- `init` - Initialize and discover dials
- `dials` - List all dials and their UIDs
- `sensors` - List all available HWInfo64 sensors
- `set <uid> <pct>` - Manually set dial position
- `color <uid> <color>` - Set dial backlight color
- `help` - Show all available commands

---

## See Also

- VUWare.HWInfo64 Library Documentation
- VUWare Console README
- HWInfo64 Official Website: https://www.hwinfo.com/
