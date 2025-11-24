# Monitoring Loop Debugging - Quick Visual Guide

## ?? The Monitoring Loop Process

```
Application Startup
        ?
Load Configuration
        ?
Initialize (Connect VU1 + HWInfo64)
        ?
"Monitoring" Status (Green) ?
        ?
?????????????????????????????????????????????????
? SensorMonitoringService.Start()               ?
?                                               ?
? MonitoringLoop runs every 1000ms              ?
? ??????????????????????????????????????????   ?
? ? For each dial:                         ?   ?
? ?   1. Read sensor from HWInfo64         ?   ?
? ?   2. Calculate percentage              ?   ?
? ?   3. Determine color                   ?   ?
? ?   4. Update VU1 dial (if changed)      ?   ?
? ?   5. Update UI button                  ?   ?
? ?                                        ?   ?
? ? Sleep 1000ms                           ?   ?
? ? Repeat                                 ?   ?
? ??????????????????????????????????????????   ?
?????????????????????????????????????????????????
        ?
Dial Buttons Update
        ?
Show percentage + color + tooltip
```

---

## ?? Points Where It Can Fail

```
Startup Sequence:
???????????????????????????????????????
? Load Config                         ? ? Wrong sensor names?
???????????????????????????????????????
? Connect VU1 Hub                     ? ? USB disconnected?
???????????????????????????????????????
? Discover Dials (I2C)                ? ? No dials found?
???????????????????????????????????????
? Connect HWInfo64                    ? ? Not running? Wrong mode?
???????????????????????????????????????
? Register Sensor Mappings            ? ? Names don't match?
???????????????????????????????????????
? Start Monitoring Loop ?             ? ? Made it here?
???????????????????????????????????????
```

---

## ?? Debug Output Messages

### ? Everything Working

```
=== HWInfo64 Diagnostics Report ===
Connected: True
Initialized: True
Available Sensors: 42

?? CPU [#0]: AMD Ryzen 7 9700X
  ?? Total CPU Usage
  ?  Value: 25.50 %

?? CPU Usage
  Status: ? MATCHED
  Value: 25.50 %
  Percentage: 25%

? Monitoring service started
  IsMonitoring: True

MonitoringLoop: Started
MonitoringLoop: Cycle 10, Updated: true
Dial updated: CPU Usage ? 25% (Cyan)
Dial updated: CPU Temperature ? 62% (Green)
```

**Result:** Dial buttons show percentages ?

---

### ? HWInfo64 Not Connected

```
=== HWInfo64 Diagnostics Report ===
Connected: False

? HWInfo64 is not connected!
Please ensure:
  1. HWInfo64 is running
  2. Running in 'Sensors only' mode
  3. 'Shared Memory Support' is enabled
```

**Fix:**
1. Start HWInfo64
2. Options ? Sensors ? Check "Shared Memory Support"
3. Restart HWInfo64

---

### ? Sensor Name Mismatch

```
?? CPU Temperature
  Sensor: CPU [#0]: AMD Ryzen 7 9700X: Enhanced
  Entry: CPU (Tctl/Tdie)
  ? Status: NOT FOUND
  
  ? Possible matches (sensor name contains):
    - CPU [#0]: AMD Ryzen 7 9700X > CPU (Tctl/Tdie)
```

**Fix:** Update config:
```json
// Change FROM:
"sensorName": "CPU [#0]: AMD Ryzen 7 9700X: Enhanced"

// Change TO:
"sensorName": "CPU [#0]: AMD Ryzen 7 9700X"
```

---

### ? Loop Running But No Updates

```
MonitoringLoop: Started
MonitoringLoop: Cycle 10, Updated: false
MonitoringLoop: Cycle 20, Updated: false
```

**Meaning:** Loop runs but sensors return null

**Possible causes:**
1. HWInfo64 not polling sensors
2. Sensor names still don't match exactly
3. HWInfo64 crashed or disconnected

**Fix:**
1. Verify HWInfo64 is running and showing sensor values
2. Double-check sensor names character-by-character
3. Restart HWInfo64

---

## ?? Testing Strategy

```
Step 1: Enable Debug Mode
  Config: "debugMode": true
                           ?
Step 2: Run Application
  dotnet run --project VUWare.App
                           ?
Step 3: Check Status Button
  Gray ? Yellow ? Yellow ? Yellow ? Green?
                           ?
                   ? Yes:    ? Continue
                   ? No:     ? Initialization failed
                   
Step 4: Check Debug Output
  Look for diagnostic report
                           ?
Step 5: Verify Sensors
  "Connected: True"?
  "Available Sensors: N"?
  Sensor status "? MATCHED"?
                           ?
                   ? All yes ? Continue
                   ? Any no  ? Fix that issue
                   
Step 6: Check Monitoring Loop
  "MonitoringLoop: Started"?
  "MonitoringLoop: Cycle 10, Updated: true"?
                           ?
                   ? Yes:    ? Check dials
                   ? No:     ? Sensors don't match
                   
Step 7: Check Dial Buttons
  Show percentages?
  Colors changing?
  Tooltips working?
                           ?
                   ? Yes:    ? SUCCESS! ?
                   ? No:     ? VU1 hub issue
```

---

## ?? UI Indicators

### Status Button
```
Gray            ? App starting
Yellow          ? Initializing
Green           ? Monitoring (loop running)
Red + Error msg ? Something failed
```

### Dial Buttons
```
Gray            ? No data yet
Green "42%"     ? Normal, showing percentage
Orange "73%"    ? Warning threshold reached
Red "88%"       ? Critical threshold reached
```

Hover for tooltip:
```
CPU Temperature
Sensor: CPU (Tctl/Tdie)
Value: 62.5 °C
Dial: 66%
Color: Green
Updates: 1234
Last: 14:32:45
```

---

## ?? Configuration Checklist

```json
{
  "dials": [
    {
      "dialUid": "290063000750524834313020",          ? Exact match from hardware
      "displayName": "CPU Usage",                     ? Friendly name
      "sensorName": "CPU [#0]: AMD Ryzen 7 9700X",   ? MUST MATCH HWInfo64 exactly
      "entryName": "Total CPU Usage",                ? MUST MATCH HWInfo64 exactly
      "minValue": 0,
      "maxValue": 100,
      "warningThreshold": 80,
      "criticalThreshold": 95,
      "colorConfig": {
        "normalColor": "Cyan",                        ? Valid color name
        "warningColor": "Yellow",
        "criticalColor": "Red"
      },
      "enabled": true,
      "updateIntervalMs": 500
    }
  ]
}
```

---

## ?? One-Minute Fix Guide

### Dials not updating?

1. **Status button is Green?**
   - NO ? Wait for initialization
   - YES ? Continue

2. **HWInfo64 running?**
   - NO ? Start it
   - YES ? Continue

3. **Debug output shows "Cycle 10, Updated: true"?**
   - NO ? Sensor name mismatch
      ```
      Fix: Copy exact sensor name from HWInfo64 Console "sensors"
      ```
   - YES ? Continue

4. **Dials updating but status changes slow?**
   ```
   Increase globalUpdateIntervalMs
   But minimum 500ms recommended
   ```

5. **Still not working?**
   ```
   Enable debug mode: "debugMode": true
   Check Debug Output for specific error
   Follow that error's fix in DEBUGGING_MONITORING_LOOP.md
   ```

---

## ?? Performance Monitoring

Monitor these in Debug Output:

```
Cycle Times:
  MonitoringLoop: Cycle 10, Updated: X
  X = number of dials that needed updating

Values (every update):
  Dial updated: CPU Usage ? 25% (Cyan)
  Shows actual percentage being sent

Errors:
  Error updating dial...
  Failed to update dial position...
  These indicate VU1 hub issues
```

---

## ? Success Criteria

You'll know it's working when:

1. ? Status button is Green ("Monitoring")
2. ? Debug output shows "MonitoringLoop: Started"
3. ? Debug output shows "Cycle X, Updated: true"
4. ? Dial buttons show percentages
5. ? Dial button colors match sensor status
6. ? Tooltips show current sensor values
7. ? Values update when you stress system
8. ? No error messages in Status button

---

## ?? Understanding the Numbers

Debug output example:
```
MonitoringLoop: Cycle 10, Updated: 2
```

Meaning:
- `Cycle 10` = 10 iterations of the loop (10 × 1000ms = 10 seconds)
- `Updated: 2` = 2 dials were updated in this cycle

This is normal - values only update when they change significantly.

---

**Ready to debug? Check the detailed guide: DEBUGGING_MONITORING_LOOP.md**
