# VUWare Console - Sensor Monitoring Quick Card

## Quick Start

```
# Step 1: Connect and Initialize
> connect
> init

# Step 2: Find your dial UID
> dials
(Copy the UID of the dial you want to use, e.g., ABC123)

# Step 3: Find available sensors
> sensors
(Find the sensor and entry names you want, e.g., "CPU Package:Temperature")

# Step 4: Monitor
> monitor ABC123 "CPU Package:Temperature"
(Watch updates in real-time)
(Press any key to stop)
```

---

## Two New Commands

### Command 1: `sensors`
Lists all HWInfo64 sensors currently available.

**Usage:**
```
sensors
```

**Output:** Shows sensors grouped by name with current, min, and max values.

---

### Command 2: `monitor`
Monitor a sensor in real-time on a VU1 dial.

**Usage:**
```
monitor <dial_uid> "<sensor_name:entry_name>" [poll_ms]
```

**Examples:**
```
monitor ABC123 "CPU Package:Temperature"
monitor XYZ789 "GPU:Load" 500
monitor DEF456 "Fan #1:Speed" 1000
```

**How to exit:**
Press any key to stop monitoring.

---

## Finding Sensor Names

Run `sensors` and look for the format:
```
[Sensor Name]
  ?? Entry Name
```

Then use in monitor command:
```
monitor <dial_uid> "Sensor Name:Entry Name"
```

---

## Auto-Scaling by Type

| Sensor Type | Scale |
|---|---|
| Temperature | 0-100°C ? 0-100% |
| Usage/Load | Already 0-100% |
| Voltage | 0-5V ? 0-100% |
| Fan | 0-5000 RPM ? 0-100% |
| Power | 0-500W ? 0-100% |

---

## Common Sensor Names

**CPU:** `CPU Package:Temperature`, `CPU Package:Load`
**GPU:** `GPU:Temperature`, `GPU:Load`
**Fans:** `Fan #1:Speed`, `Fan #2:Speed`

Use `sensors` command to see exact names on your system!

---

## Poll Interval Guide

```
100-250ms  ? High detail (more CPU)
500ms      ? Default (good balance)
1000ms     ? Standard (less CPU)
2000-5000ms ? Slow monitoring
```

---

## Requirements

- HWInfo64 running in "Sensors only" mode
- "Shared Memory Support" enabled in HWInfo64 Options
- VU1 initialized with `init` command

---

## Troubleshooting

| Problem | Solution |
|---|---|
| "Dial not found" | Run `dials` to see correct UIDs |
| "Sensor not found" | Run `sensors` and copy exact names |
| HWInfo64 error | Enable Shared Memory in HWInfo64 Options |
| Slow updates | Reduce poll interval (e.g., 250ms) |
| Fast updates | Increase poll interval (e.g., 2000ms) |

---

## Full Help

```
> help
```

Shows all available commands including detailed information.

---

## Related Commands

```
dials         # List all dials with UIDs
set <uid> <%> # Manually set dial position
color <uid>   # Set dial backlight color
help          # Show all commands
```

---

## Tips & Tricks

1. **Use Quotes** - Always quote sensor names with spaces
   ```
   monitor ABC123 "CPU Package:Temperature" ?
   monitor ABC123 CPU Package:Temperature ?
   ```

2. **Multiple Monitors** - Open multiple console windows to monitor different sensors

3. **Fast Updates** - Reduce interval for responsive dials
   ```
   monitor ABC123 "GPU:Load" 250
   ```

4. **Slow Polling** - Increase interval for low CPU usage
   ```
   monitor ABC123 "CPU Package:Temperature" 2000
   ```

---

## Example Session

```
C:\> VUWare.Console.exe

> connect
? Connected to VU1 Hub!

> init
? Initialized! Found 2 dial(s).

> dials
? [1] CPU Temp Monitor      UID: ABC123
? [2] GPU Temp Monitor      UID: XYZ789

> sensors
? [CPU Package]
?   ?? Temperature
?   ?  Value: 45.50 °C
?   ?? Load
?   ?  Value: 12.30 %

> monitor ABC123 "CPU Package:Temperature"

?? Sensor Monitor ???????????????????????
? Press any key to stop monitoring...   ?
?????????????????????????????????????????

[12:34:56] Value: 45.3 °C  ? Dial: 45%
[12:34:57] Value: 46.1 °C  ? Dial: 46%
[12:34:58] Value: 46.8 °C  ? Dial: 47%

? Monitoring ended. Sent 3 updates to dial.

> exit
```

---

## Documentation

For detailed information, see:
- `SENSOR_MONITORING_GUIDE.md` - Complete guide
- `HWINFO64_INTEGRATION.md` - Integration details
- `ENHANCEMENT_SUMMARY.md` - Technical summary

---

**Ready to monitor your system!** ??
