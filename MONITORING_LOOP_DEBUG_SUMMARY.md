# Sensor Monitoring Loop - Issue Investigation & Debug Tools

## ?? Investigation Summary

I've analyzed the sensor monitoring loop implementation and identified the most likely reasons why it might not be running:

---

## ?? Common Issues That Prevent Monitoring

### 1. **HWInfo64 Not Connected**
- HWInfo64 not running
- Not in "Sensors only" mode
- "Shared Memory Support" not enabled

**Impact:** Loop runs but can't read sensors ? No updates to dials

---

### 2. **Sensor Name Mismatch**
- Config sensor name doesn't match HWInfo64 exactly
- Example:
  ```
  Config:   "CPU [#0]: AMD Ryzen 7 9700X: Enhanced"
  HWInfo64: "CPU [#0]: AMD Ryzen 7 9700X"
  Result:   No match ? No sensor readings
  ```

**Impact:** Loop runs but sensors return null ? No dial updates

---

### 3. **VU1 Hub Not Connected**
- USB cable disconnected
- Hub not powered
- Device driver not installed

**Impact:** Dials won't update even if sensor readings work

---

### 4. **Configuration Issues**
- Dial UID doesn't match actual hardware
- Entry name misspelled or wrong case
- Invalid JSON syntax

**Impact:** Configuration won't load or sensors won't match

---

## ??? Tools Added for Debugging

### 1. **DiagnosticsService** (`Services/DiagnosticsService.cs`)

Provides detailed diagnostic information:

```csharp
var diagnostics = new DiagnosticsService(hwInfoController);

// Get full report
string report = diagnostics.GetDiagnosticsReport();
// Shows: Connected status, available sensors, registered mappings,
// sensor name matching, percentage calculations

// Get status summary
string status = diagnostics.GetStatusSummary();
// Example: "? 4/4 sensors matched"

// Validate mappings
List<string> issues = diagnostics.ValidateSensorMappings();
// Lists all sensor matching problems
```

**Output Example:**
```
=== HWInfo64 Diagnostics Report ===
Connected: True
Initialized: True

Available Sensors: 42

?? CPU [#0]: AMD Ryzen 7 9700X
  ?? Total CPU Usage
  ?  Value: 25.50 %

?? CPU Usage
  ID: 290063000750524834313020
  Sensor: CPU [#0]: AMD Ryzen 7 9700X
  Entry: Total CPU Usage
  ? Status: MATCHED
  Value: 25.50 %
  Percentage: 25%
```

---

### 2. **Enhanced Logging in MainWindow**

When debug mode is enabled (`"debugMode": true`):

```csharp
// Automatically prints diagnostics report on startup
// Shows sensor matching details before monitoring starts
// Helps identify sensor name mismatches immediately
```

**Output Example:**
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
```

---

### 3. **Enhanced Logging in SensorMonitoringService**

Detailed loop execution logging:

```csharp
// MonitoringLoop entry/exit messages
"MonitoringLoop: Started"
"MonitoringLoop: Cycle 10, Updated: true"
"Dial updated: CPU Temperature ? 70% (Green)"

// Tracks:
// - Loop cycles
// - Number of dials updated per cycle
// - Actual dial values and colors being set
// - Any errors that occur
```

**This helps identify:**
- Is the loop running at all?
- Are sensor readings being received?
- Are dials actually being updated?
- What are the actual values being sent?

---

## ?? How to Use the Debug Tools

### Step 1: Enable Debug Mode

Edit `Config/dials-config.json`:
```json
{
  "appSettings": {
    "debugMode": true
  }
}
```

### Step 2: Open Debug Output

In Visual Studio:
- `View ? Output` (or `Ctrl+Alt+O`)
- Select "Debug" from the dropdown

### Step 3: Run the App

```bash
dotnet run --project VUWare.App
```

### Step 4: Check Debug Output

Look for messages like:

**? Successful:**
```
HWInfo64 Diagnostics Report ===
Connected: True
?? CPU [#0]: AMD Ryzen 7 9700X
  ?? Total CPU Usage
    Value: 25.50 %
?? CPU Usage
  ? Status: MATCHED
MonitoringLoop: Started
MonitoringLoop: Cycle 10, Updated: true
Dial updated: CPU Usage ? 25% (Cyan)
```

**? Problem - HWInfo64 not connected:**
```
Connected: False
? HWInfo64 is not connected!
Please ensure:
  1. HWInfo64 is running
  2. Running in 'Sensors only' mode
  3. 'Shared Memory Support' is enabled
```

**? Problem - Sensor name mismatch:**
```
?? CPU Temperature
  Sensor: CPU [#0]: AMD Ryzen 7 9700X: Enhanced
  Entry: CPU (Tctl/Tdie)
  ? Status: NOT FOUND
  ? Possible matches:
    - CPU [#0]: AMD Ryzen 7 9700X > CPU (Tctl/Tdie)
```

---

## ?? Quick Fix Workflow

1. **Enable debug mode** in config
2. **Run the app**
3. **Check Debug Output** for issues
4. **Fix based on error messages:**
   - HWInfo not connected? ? Start HWInfo64
   - Sensor not found? ? Correct sensor names in config
   - Dials not updating? ? Check USB connection

5. **Restart app** and verify dials update

---

## ?? Monitoring Loop Operation

The monitoring loop works as follows:

```
???????????????????????????????????????????
? Loop Cycle (every 1000ms)               ?
???????????????????????????????????????????
?                                         ?
? For each dial in configuration:         ?
?                                         ?
?  1. Get sensor reading from HWInfo64    ?
?     ?? If null ? Skip this dial         ?
?                                         ?
?  2. Calculate percentage                ?
?     Percentage = (Value-Min)/(Max-Min)*100
?                                         ?
?  3. Determine color                     ?
?     ?? Based on thresholds              ?
?                                         ?
?  4. If value or color changed:          ?
?     ?? Update dial position             ?
?     ?? Update dial color                ?
?     ?? Update UI button                 ?
?     ?? Fire OnDialUpdated event         ?
?                                         ?
? Sleep 1000ms                            ?
? Repeat                                  ?
???????????????????????????????????????????
```

---

## ?? Key Points

1. **Debug mode must be enabled** in config to see diagnostics
2. **HWInfo64 must be connected** for sensor readings
3. **Sensor names must match exactly** (case-sensitive)
4. **Debug output shows detailed step-by-step** execution
5. **Error messages are very specific** about what's wrong

---

## ?? Documentation

New debugging documentation:
- **DEBUGGING_MONITORING_LOOP.md** - Comprehensive troubleshooting guide
- Includes common issues and fixes
- Step-by-step diagnostic process
- Example debug output for all scenarios

---

## ? Files Modified

- ? `MainWindow.xaml.cs` - Added diagnostics logging
- ? `SensorMonitoringService.cs` - Added detailed loop logging
- ? `DiagnosticsService.cs` - New diagnostic service
- ? `DEBUGGING_MONITORING_LOOP.md` - New comprehensive guide

---

## ?? Next Steps to Fix Your Issue

1. **Enable debug mode** in `Config/dials-config.json`
2. **Verify HWInfo64** is running in Sensors-only mode with Shared Memory Support
3. **Run the app** and check Debug Output
4. **Look for specific error message** from diagnostics
5. **Follow the fix** for that specific issue
6. **Restart and verify**

---

## Build Status

? **Project builds successfully with all new code**

Ready to run and debug!
