# VUWare.App - Sensor Monitoring Loop Debugging Guide

## Problem: Monitoring Loop Not Running

If the sensor monitoring loop is not running, here are the steps to diagnose and fix the issue.

---

## ?? Diagnostic Checklist

### 1. **HWInfo64 Connection**
- [ ] HWInfo64 is running on the system
- [ ] HWInfo64 is running in **"Sensors only"** mode (NOT "Sensors and System Info")
- [ ] **"Shared Memory Support"** is enabled in HWInfo64 Options
- [ ] No error messages about connection failure

**Check:** Look for error message "Failed to connect to HWInfo64" in the UI

---

### 2. **Sensor Names Match Exactly**
- [ ] Sensor name in config matches HWInfo64 **exactly** (case-sensitive in some cases)
- [ ] Entry name in config matches HWInfo64 **exactly**
- [ ] No extra spaces or special characters

**Example Issue:**
```
Config says:   "CPU [#0]: AMD Ryzen 7 9700X"
HWInfo64 has:  "CPU [#0]: AMD Ryzen 7 9700X: Enhanced"  ? MISMATCH!
```

---

### 3. **Application Initialization**
- [ ] Status button progresses: Yellow ? Yellow ? Yellow ? Green
- [ ] Status button turns Green ("Monitoring")
- [ ] No error messages appear

**Check:** If Status button shows error, hover over it to see the message

---

### 4. **Debug Mode Enabled**
Enable debug logging to see detailed information:

```json
{
  "appSettings": {
    "debugMode": true
  }
}
```

This will show:
- Configuration loaded
- Sensors registered
- Monitoring loop cycle counts
- Dial updates

---

## ??? Troubleshooting Steps

### Step 1: Enable Debug Mode

Edit `Config/dials-config.json`:
```json
"debugMode": true
```

### Step 2: Run the Application

```bash
dotnet run --project VUWare.App
```

### Step 3: Check Debug Output

Open Visual Studio's **Output** window (`View ? Output` or `Ctrl+Alt+O`)

Look for messages like:
```
HWInfo64 Diagnostics Report ===
Connected: True
Initialized: True
Available Sensors: 42

?? CPU [#0]: AMD Ryzen 7 9700X
  ?? Total CPU Usage
  ?  Value: 25.50 %
  
?? CPU Temperature
  Sensor: CPU [#0]: AMD Ryzen 7 9700X: Enhanced
  Entry: CPU (Tctl/Tdie)
  ? Status: MATCHED
  Value: 62.50 °C
  Percentage: 70%

MonitoringLoop: Started
MonitoringLoop: Cycle 10, Updated: true
Dial updated: CPU Temperature ? 70% (Green)
```

### Step 4: Identify the Issue

#### Issue: "HWInfo64 not connected"
```
? HWInfo64 is not connected!
Please ensure:
  1. HWInfo64 is running
  2. Running in 'Sensors only' mode
  3. 'Shared Memory Support' is enabled in Options
```

**Fix:**
1. Start HWInfo64
2. Switch to "Sensors only" mode
3. Go to HWInfo64 Options ? Sensors ? Check "Shared Memory Support"
4. Restart HWInfo64
5. Restart the app

---

#### Issue: "No sensors available"
```
? No sensors available in HWInfo64
```

**Fix:**
1. HWInfo64 must be running with sensors available
2. Make sure it's in "Sensors only" mode with data visible
3. Restart HWInfo64

---

#### Issue: "Sensor not found"
```
?? CPU Temperature
  Sensor: CPU [#0]: AMD Ryzen 7 9700X: Enhanced
  Entry: CPU (Tctl/Tdie)
  ? Status: NOT FOUND
  ? Possible matches (sensor name contains):
    - CPU [#0]: AMD Ryzen 7 9700X > Total CPU Usage
    - CPU Package > Temperature
```

**Fix:** Correct the sensor name in config:
1. Check HWInfo64 for the exact sensor names
2. Copy the exact name including spaces and special characters
3. Update `dials-config.json`
4. Restart the app

---

#### Issue: "MonitoringLoop: Started" but no updates
```
MonitoringLoop: Started
MonitoringLoop: Cycle 10, Updated: false
MonitoringLoop: Cycle 20, Updated: false
```

This means the loop is running but sensors aren't being read.

**Possible causes:**
1. Sensors matched but return null
2. Sensor readings aren't updating
3. Check if HWInfo64 is actually polling sensors

**Fix:**
1. Verify HWInfo64 is running and polling
2. Check that sensor values are changing in HWInfo64
3. Try restarting HWInfo64

---

### Step 5: Use the Console App for Verification

Use the Console app to verify sensors are available:

```bash
dotnet run --project VUWare.Console
> sensors
```

This will list all available sensors and their exact names.

Compare these names with your config file.

---

## ?? Detailed Debug Output Explanation

### Successful Startup
```
HWInfo64 Diagnostics Report ===
Connected: True
Initialized: True
Poll Interval: 1000ms

Available Sensors: 42
...
? Monitoring service started
  IsMonitoring: True
=== Starting Monitoring Service ===
MonitoringLoop: Started
```

**Next:** You should see cycle messages every 10 cycles:
```
MonitoringLoop: Cycle 10, Updated: true
Dial updated: CPU Temperature ? 70% (Green)
```

---

### Common Error Patterns

#### Pattern 1: Sensor Name Mismatch
```
?? CPU Temperature
  Sensor: CPU [#0]: AMD Ryzen 7 9700X: Enhanced
  Entry: CPU (Tctl/Tdie)
  ? Status: NOT FOUND
  ? Found entries with matching name:
    - CPU [#0]: AMD Ryzen 7 9700X > CPU (Tctl/Tdie)
```

**Solution:** Update sensor name in config:
```json
"sensorName": "CPU [#0]: AMD Ryzen 7 9700X"  // Remove ": Enhanced"
```

---

#### Pattern 2: HWInfo64 Not Running
```
Connected: False
? HWInfo64 is not connected!
```

**Solution:**
1. Start HWInfo64
2. Ensure Shared Memory Support is enabled
3. Make sure it's in Sensors-only mode
4. Restart the app

---

#### Pattern 3: Sensor Registered but Not Matched
```
?? CPU Usage
  ID: 290063000750524834313020
  Sensor: CPU [#0]: AMD Ryzen 7 9700X
  Entry: Total CPU Usage
  ? Status: NOT FOUND
```

**Solution:**
1. Run Console app and check `sensors` command
2. Find the exact sensor name
3. Copy it exactly into config
4. Restart app

---

## ?? Testing the Fix

After making changes:

1. **Restart HWInfo64** if you changed settings
2. **Restart the app**
3. **Watch the Status button**: Should go Yellow ? Green
4. **Check Debug Output**: Should see "MonitoringLoop: Started"
5. **Check Dial Buttons**: Should update with percentages
6. **Verify Tooltips**: Hover over buttons to see sensor info

---

## ?? Common Fixes

### Fix 1: Correct Sensor Names
```json
// BEFORE (Wrong):
"sensorName": "CPU [#0]: AMD Ryzen 7 9700X: Enhanced",
"entryName": "CPU (Tctl/Tdie)"

// AFTER (Correct):
"sensorName": "CPU [#0]: AMD Ryzen 7 9700X: Enhanced",  // Exact match from HWInfo64
"entryName": "CPU (Tctl/Tdie)"  // Exact match from HWInfo64
```

### Fix 2: Enable HWInfo64 Shared Memory
1. Start HWInfo64
2. Options ? Sensors tab
3. Check "Shared Memory Support"
4. Click OK
5. Restart HWInfo64

### Fix 3: Switch HWInfo64 Mode
1. HWInfo64 ? View menu
2. Select "Sensors only"
3. Restart the app

### Fix 4: Check USB Connection
If dials aren't updating even though monitoring says OK:
1. Check VU1 Hub is connected via USB
2. Check Device Manager for USB device
3. Power cycle the hub and dials

---

## ?? Debug Configuration

Recommended debug config for troubleshooting:

```json
{
  "version": "1.0",
  "appSettings": {
    "autoConnect": true,
    "enablePolling": true,
    "globalUpdateIntervalMs": 1000,
    "logFilePath": "",
    "debugMode": true  // ? Enable this
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

Use only ONE dial for testing to simplify debugging.

---

## ?? Verification Checklist

Once you think it's fixed, verify:

- [ ] Status button is Green ("Monitoring")
- [ ] Debug output shows "MonitoringLoop: Started"
- [ ] Debug output shows dial updates (every 10 cycles)
- [ ] Dial buttons show percentages
- [ ] Button colors change when you stress the system (CPU load)
- [ ] Tooltips show current sensor values

---

## ?? Getting Help

If you still can't get it working:

1. **Take a screenshot** of the Debug Output window
2. **Note the error messages** in Status button
3. **Run the Console app** and save the `sensors` output
4. **Compare sensor names** in config vs. HWInfo64
5. **Check that HWInfo64** is in correct mode with Shared Memory enabled

---

## Summary

The monitoring loop requires:
1. ? HWInfo64 running in Sensors-only mode
2. ? Shared Memory Support enabled in HWInfo64
3. ? Correct sensor names in dials-config.json
4. ? VU1 Hub connected and initialized

If all four are met, the monitoring loop WILL run and update dials in real-time.
